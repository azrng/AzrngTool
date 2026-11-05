using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Timer = System.Timers.Timer;

namespace AzrngTools.Services;

/// <summary>
/// 页面输入记忆存档实现（DES-SETTINGS-002 / CON-SETTINGS-001）：一页一文件落
/// %LOCALAPPDATA%\AzrngTools\page-inputs\&lt;pageId&gt;.json。Save 只更新内存快照并重启防抖
/// （默认 800ms）合并连续键入，到期逐页原子落盘；进程退出挂钩限时冲刷兜底。
/// 读取为同步小文件读取，页面构造时调用无感知延迟；损坏文件整页兜底为空。
/// </summary>
public sealed partial class PageInputStoreService : IPageInputStoreService, ISingletonDependency
{
    private const int DebounceIntervalMs = 800;
    private const int MaxValueLength = 2_000_000;
    private const int ProcessExitFlushBudgetMs = 2000;
    private const int CurrentArchiveVersion = 1;

    private readonly object _flushLock = new();
    private readonly string _directory;
    private readonly Timer _saveTimer;
    private readonly ConcurrentDictionary<string, PageInputArchive> _pendingPages = new(StringComparer.Ordinal);
    private bool _processExitHooked;

    public PageInputStoreService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools",
            "page-inputs"))
    {
    }

    // 供测试注入独立目录，避免读写真实用户数据
    internal PageInputStoreService(string directory)
    {
        _directory = directory;
        _saveTimer = new Timer(DebounceIntervalMs) { AutoReset = false };
        _saveTimer.Elapsed += (_, _) => FlushPendingPages();
        HookProcessExitOnce();
    }

    /// <summary>提交页面输入快照：只更新内存与重启防抖，不做磁盘 IO，UI 线程可安全调用。</summary>
    public void Save(string pageId, IReadOnlyDictionary<string, string?> inputs)
    {
        if (string.IsNullOrWhiteSpace(pageId) || !IsValidPageId(pageId) || inputs.Count == 0)
        {
            return;
        }

        _pendingPages[pageId] = BuildArchive(inputs);

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public IReadOnlyDictionary<string, string?> TryLoad(string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId) || !IsValidPageId(pageId))
        {
            return new Dictionary<string, string?>();
        }

        var filePath = GetPageFilePath(pageId);
        try
        {
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, string?>();
            }

            var json = File.ReadAllText(filePath);
            var archive = JsonSerializer.Deserialize(json, PageInputJsonContext.Default.PageInputArchive);
            // 版本号不认识视同损坏：未来结构升级后旧读取方按无存档兜底，避免错位解析
            if (archive is null || archive.Version != CurrentArchiveVersion)
            {
                AppLog.LogError($"页面输入存档版本不兼容，按无存档处理: {filePath}");
                return new Dictionary<string, string?>();
            }

            return archive.Inputs;
        }
        catch (Exception ex)
        {
            AppLog.LogError($"读取页面输入存档失败: {filePath} {ex.Message}\n{ex.GetExceptionAndStack()}");
            return new Dictionary<string, string?>();
        }
    }

    /// <summary>组装存档快照：单值超限（数 MB 级输入不值得存）跳过该键并留痕，其余键值保留。</summary>
    private PageInputArchive BuildArchive(IReadOnlyDictionary<string, string?> inputs)
    {
        var filtered = new Dictionary<string, string?>(inputs.Count, StringComparer.Ordinal);
        foreach (var (key, value) in inputs)
        {
            if (value is not null && value.Length > MaxValueLength)
            {
                AppLog.LogError($"页面输入存档跳过超大值: {key}（{value.Length} 字符）");
                continue;
            }

            filtered[key] = value;
        }

        return new PageInputArchive
        {
            Version = CurrentArchiveVersion,
            UpdatedAt = DateTime.UtcNow,
            Inputs = filtered
        };
    }

    /// <summary>
    /// 防抖到期落盘：写失败的页快照放回待写集合等下一次 Save 或退出冲刷；TryAdd 而非索引器，
    /// 避免覆盖冲刷期间同页 Save 提交的更新快照。
    /// </summary>
    private void FlushPendingPages()
    {
        lock (_flushLock)
        {
            foreach (var pageId in _pendingPages.Keys)
            {
                if (!_pendingPages.TryRemove(pageId, out var archive))
                {
                    continue;
                }

                try
                {
                    WriteArchive(pageId, archive);
                }
                catch (Exception ex)
                {
                    AppLog.LogError($"写入页面输入存档失败: {pageId} {ex.Message}\n{ex.GetExceptionAndStack()}");
                    _pendingPages.TryAdd(pageId, archive);
                }
            }
        }
    }

    private void WriteArchive(string pageId, PageInputArchive archive)
    {
        Directory.CreateDirectory(_directory);
        var filePath = GetPageFilePath(pageId);
        var json = JsonSerializer.Serialize(archive, PageInputJsonContext.Default.PageInputArchive);
        var tempFilePath = filePath + ".tmp";
        File.WriteAllText(tempFilePath, json);
        File.Move(tempFilePath, filePath, true);
    }

    /// <summary>进程退出冲刷：限时逐页落盘，超时放弃剩余页（ProcessExit 预算有限，避免拖慢关闭）。</summary>
    private void FlushOnProcessExit()
    {
        try
        {
            var deadlineTicks = Environment.TickCount64 + ProcessExitFlushBudgetMs;
            lock (_flushLock)
            {
                while (_pendingPages.Count > 0 && Environment.TickCount64 < deadlineTicks)
                {
                    var pageId = _pendingPages.Keys.First();
                    if (!_pendingPages.TryRemove(pageId, out var archive))
                    {
                        continue;
                    }

                    try
                    {
                        WriteArchive(pageId, archive);
                    }
                    catch (Exception ex)
                    {
                        AppLog.LogError($"退出冲刷页面输入存档失败: {pageId} {ex.Message}");
                    }
                }
            }
        }
        catch
        {
            // 退出阶段吞掉一切异常，避免干扰进程关闭
        }
    }

    private void HookProcessExitOnce()
    {
        lock (_flushLock)
        {
            if (_processExitHooked)
            {
                return;
            }

            _processExitHooked = true;
        }

        AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushOnProcessExit();
    }

    private string GetPageFilePath(string pageId)
    {
        return Path.Combine(_directory, pageId + ".json");
    }

    // pageId 同时是文件名，调用点都是代码内常量，这里仍防御非法字符避免拼出意外路径
    private static bool IsValidPageId(string pageId)
    {
        return pageId.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }

    /// <summary>测试钩子：立即同步冲刷全部待写页，保证断言前落盘结果确定可见。</summary>
    internal void FlushPendingPagesForTests()
    {
        FlushPendingPages();
    }

    /// <summary>测试钩子：当前待写页数量，用于断言恢复赋值未触发保存提交。</summary>
    internal int PendingPageCountForTests => _pendingPages.Count;

    // 源生成 JSON 上下文：AOT 发布下不能用反射序列化；camelCase 对齐 CON-SETTINGS-001 契约
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(PageInputArchive))]
    private sealed partial class PageInputJsonContext : JsonSerializerContext
    {
    }

    private sealed class PageInputArchive
    {
        public int Version { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Dictionary<string, string?> Inputs { get; set; } = [];
    }
}
