using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AzrngTools.Utils;

/// <summary>
/// 应用本地日志器：等价替换外部包 Azrng.Core 的本地日志实现，落盘根目录迁移到
/// %LOCALAPPDATA%\AzrngTools\logs（与主题、统计等其他数据同处应用数据目录）。
/// 行为对齐原实现：调用只入队即返回、不阻塞业务线程，后台定时批量落盘，
/// 进程退出限时冲刷，过期日志每日清理；日志链路所有 IO 异常吞掉，绝不影响业务。
/// </summary>
public static class AppLog
{
    /// <summary>错误日志类型标记，该类型条目除按天文件外同时追加 error.log。</summary>
    private const string ErrorType = "错误";

    /// <summary>待写队列上限：持续写失败时丢最旧条目，防止内存膨胀。</summary>
    private const int MaxPendingCount = 10000;

    /// <summary>后台批量落盘间隔（毫秒）。</summary>
    private const int FlushIntervalMs = 500;

    /// <summary>日志文件保留天数，超期 .log（含 error.log）每日清理一次。</summary>
    private const int RetentionDays = 30;

    /// <summary>进程退出冲刷预算（毫秒）：ProcessExit 预算有限，必须限时。</summary>
    private const int ProcessExitFlushBudgetMs = 2000;

    private static readonly object WriteLock = new();
    private static readonly object StateLock = new();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly ConcurrentQueue<LogEntry> PendingEntries = new();

    /// <summary>待写条目计数，配合队列实现“满时丢最旧”。</summary>
    private static long _pendingCount;

    private static Task? _consumerTask;
    private static CancellationTokenSource? _consumerCancellation;
    private static bool _processExitHooked;

    private static string _logDirectory = GetDefaultLogDirectory();

    /// <summary>上次过期清理日期，按天去重保证每日只清理一次。</summary>
    private static DateTime _lastCleanupDate = DateTime.MinValue;

    private readonly record struct LogEntry(DateTime Timestamp, string Type, string Info);

    /// <summary>写入一条业务日志（对等替换原外部包 WriteMyLogs 入口）。</summary>
    public static void WriteMyLogs(string type, string info)
    {
        Enqueue(new LogEntry(DateTime.Now, type, info));
    }

    /// <summary>记录错误：进按天日志文件，同时追加同目录 error.log（对等替换原外部包 LogError 入口）。</summary>
    public static void LogError(string info)
    {
        Enqueue(new LogEntry(DateTime.Now, ErrorType, info));
    }

    /// <summary>日志默认根目录：%LOCALAPPDATA%\AzrngTools\logs。</summary>
    private static string GetDefaultLogDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools",
            "logs");
    }

    /// <summary>入队即返回；队列满时丢最旧条目，首次入队懒启动后台写者与退出冲刷钩子。</summary>
    private static void Enqueue(LogEntry entry)
    {
        try
        {
            // 持续写失败场景下宁可丢日志也不能撑爆内存，丢弃最旧条目给新条目让位
            while (Interlocked.Read(ref _pendingCount) >= MaxPendingCount)
            {
                if (!TryDequeue(out _))
                {
                    break;
                }
            }

            PendingEntries.Enqueue(entry);
            Interlocked.Increment(ref _pendingCount);
            EnsureBackgroundWorker();
        }
        catch (Exception ex)
        {
            // 日志器自身任何异常都不允许影响业务线程
            Debug.WriteLine($"AppLog 入队失败: {ex.Message}");
        }
    }

    private static bool TryDequeue(out LogEntry entry)
    {
        if (PendingEntries.TryDequeue(out entry))
        {
            Interlocked.Decrement(ref _pendingCount);
            return true;
        }

        return false;
    }

    /// <summary>首次使用时懒启动后台写者，并挂进程退出冲刷钩子。</summary>
    private static void EnsureBackgroundWorker()
    {
        lock (StateLock)
        {
            if (!_processExitHooked)
            {
                _processExitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushOnProcessExit();
            }

            if (_consumerTask is not null)
            {
                return;
            }

            _consumerCancellation = new CancellationTokenSource();
            _consumerTask = Task.Run(() => ConsumerLoopAsync(_consumerCancellation.Token));
        }
    }

    /// <summary>后台写者主循环：按固定间隔醒来做过期清理与批量落盘。</summary>
    private static async Task ConsumerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(FlushIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                CleanupExpiredFilesIfNeeded();
                DrainPendingEntries();
            }
            catch (Exception ex)
            {
                // 后台写者兜底：单轮异常只丢本轮，下轮继续，保证日志器自身永不下沉异常
                Debug.WriteLine($"AppLog 后台写日志异常: {ex.Message}");
            }
        }
    }

    /// <summary>把当前队列批量落盘；写失败时把尚未写入的条目重新入队等待下轮重试。</summary>
    private static void DrainPendingEntries()
    {
        var batch = new List<LogEntry>();
        while (TryDequeue(out var entry))
        {
            batch.Add(entry);
        }

        if (batch.Count == 0)
        {
            return;
        }

        var writtenCount = 0;
        try
        {
            lock (WriteLock)
            {
                for (var index = 0; index < batch.Count; index++)
                {
                    WriteEntry(batch[index]);
                    writtenCount = index + 1;
                }
            }
        }
        catch (Exception ex)
        {
            // 典型场景是日志文件被占用：本批未写入的条目重新入队等下轮，持续失败由队列上限自然丢弃；
            // ConcurrentQueue 只能尾部追加，极端并发下重入队条目与新增条目的顺序可能略有差异
            Debug.WriteLine($"AppLog 落盘失败: {ex.Message}");
            for (var index = writtenCount; index < batch.Count; index++)
            {
                PendingEntries.Enqueue(batch[index]);
                Interlocked.Increment(ref _pendingCount);
            }
        }
    }

    /// <summary>单条落盘：按天文件追加一行；错误类型同时追加 error.log。</summary>
    private static void WriteEntry(LogEntry entry)
    {
        // 快照目录，避免写文件中途被测试钩子切换目录导致两段写进不同目录
        var logDirectory = _logDirectory;

        // 目录懒创建：首次写入时才建目录
        Directory.CreateDirectory(logDirectory);

        var line = $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Type}] {entry.Info}{Environment.NewLine}";
        var dailyFilePath = Path.Combine(logDirectory, entry.Timestamp.ToString("yyyyMMdd") + ".log");
        File.AppendAllText(dailyFilePath, line, Utf8NoBom);

        if (entry.Type == ErrorType)
        {
            File.AppendAllText(Path.Combine(logDirectory, "error.log"), line, Utf8NoBom);
        }
    }

    /// <summary>过期清理：按天去重，删除保留期外的 .log 文件（含 error.log）。</summary>
    private static void CleanupExpiredFilesIfNeeded()
    {
        var today = DateTime.Now.Date;
        lock (StateLock)
        {
            if (_lastCleanupDate == today)
            {
                return;
            }

            _lastCleanupDate = today;
        }

        try
        {
            var expiredBefore = DateTime.Now.AddDays(-RetentionDays);
            foreach (var filePath in Directory.EnumerateFiles(_logDirectory, "*.log"))
            {
                if (File.GetLastWriteTime(filePath) < expiredBefore)
                {
                    File.Delete(filePath);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppLog 清理过期日志失败: {ex.Message}");
        }
    }

    /// <summary>进程退出冲刷：限时逐条落盘，超时后剩余条目放弃（ProcessExit 预算有限）。</summary>
    private static void FlushOnProcessExit()
    {
        try
        {
            var deadlineTicks = Environment.TickCount64 + ProcessExitFlushBudgetMs;
            while (Environment.TickCount64 < deadlineTicks && TryDequeue(out var entry))
            {
                try
                {
                    lock (WriteLock)
                    {
                        WriteEntry(entry);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppLog 退出冲刷单条失败: {ex.Message}");
                }
            }
        }
        catch
        {
            // 退出阶段吞掉一切异常，避免干扰进程关闭
        }
    }

    /// <summary>测试钩子：切换日志目录并重置队列、后台写者等静态状态，避免测试串扰。</summary>
    internal static void ConfigureForTests(string logDirectory)
    {
        Task? staleConsumer;
        CancellationTokenSource? staleCancellation;
        lock (StateLock)
        {
            staleConsumer = _consumerTask;
            staleCancellation = _consumerCancellation;
            _consumerTask = null;
            _consumerCancellation = null;
            _logDirectory = logDirectory;
            _lastCleanupDate = DateTime.MinValue;
        }

        // 停掉旧写者并等它退出，保证切换目录后没有针对旧目录的在途写入
        if (staleConsumer is not null && staleCancellation is not null)
        {
            staleCancellation.Cancel();
            try
            {
                staleConsumer.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // 旧写者异常退出不影响测试钩子继续执行
            }
        }

        staleCancellation?.Dispose();

        // 丢弃遗留条目，避免上一场景的日志串到新目录
        while (TryDequeue(out _))
        {
        }
    }

    /// <summary>测试钩子：停掉后台写者后同步排空队列并触发一次过期清理，保证断言前落盘结果确定可见。</summary>
    internal static void FlushPendingForTests()
    {
        Task? consumer;
        CancellationTokenSource? cancellation;
        lock (StateLock)
        {
            consumer = _consumerTask;
            cancellation = _consumerCancellation;
            _consumerTask = null;
            _consumerCancellation = null;
        }

        if (consumer is not null && cancellation is not null)
        {
            cancellation.Cancel();
            try
            {
                consumer.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // 后台写者异常退出不影响排空继续
            }

            cancellation.Dispose();
        }

        lock (WriteLock)
        {
            while (TryDequeue(out var entry))
            {
                try
                {
                    WriteEntry(entry);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AppLog 测试冲刷单条失败: {ex.Message}");
                }
            }
        }

        // 顺带触发一次过期清理，让清理逻辑可在测试中确定性验证
        CleanupExpiredFilesIfNeeded();
    }
}
