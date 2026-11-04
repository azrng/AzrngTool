using System.Diagnostics;

namespace AzrngTools.Utils;

/// <summary>
/// 性能计时作用域：Dispose 时耗时达到阈值才写本地性能日志，快路径零日志输出；
/// 用于检测真实使用中的慢操作，与无头性能探针（AzrngTools.Tests/Performance）互补——
/// 探针管回归基线，这里管实机真实输入。
/// </summary>
public sealed class PerfLogScope : IDisposable
{
    /// <summary>默认阈值：超过 200ms 视为用户可感知的慢操作。</summary>
    public const int DefaultThresholdMs = 200;

    private readonly string _operation;
    private readonly string? _context;
    private readonly Func<string?>? _contextFactory;
    private readonly long _thresholdMs;
    private readonly Stopwatch _stopwatch;

    private PerfLogScope(string operation, string? context, Func<string?>? contextFactory, long thresholdMs)
    {
        _operation = operation;
        _context = context;
        _contextFactory = contextFactory;
        _thresholdMs = thresholdMs;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// 开始计时。thresholdMs 传 0 表示无条件记录（用于启动、更新下载等诊断价值恒定的链路）。
    /// </summary>
    public static PerfLogScope Start(string operation, string? context = null, int thresholdMs = DefaultThresholdMs)
    {
        return new PerfLogScope(operation, context, null, thresholdMs);
    }

    /// <summary>
    /// 开始计时，上下文延迟到落日志时才求值：适用于上下文要等操作结束才能确定的场景（如 HTTP 响应体规模）。
    /// 只有耗时超过阈值时才调用工厂，快路径不产生额外字符串。
    /// </summary>
    public static PerfLogScope Start(string operation, Func<string?> contextFactory, int thresholdMs = DefaultThresholdMs)
    {
        return new PerfLogScope(operation, null, contextFactory, thresholdMs);
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        if (_stopwatch.ElapsedMilliseconds < _thresholdMs)
        {
            return;
        }

        var contextValue = _contextFactory?.Invoke() ?? _context;
        var context = contextValue is null ? string.Empty : $"（{contextValue}）";
        AppLog.WriteMyLogs("性能检测", $"{_operation} 耗时 {_stopwatch.ElapsedMilliseconds}ms{context}");
    }

    /// <summary>输入规模描述：文本字符数或文件字节数换算为可读 KB/MB，仅用于日志上下文。</summary>
    public static string DescribeSize(long characterOrByteCount)
    {
        return characterOrByteCount >= 1024 * 1024
            ? $"{characterOrByteCount / (1024.0 * 1024.0):0.#}MB"
            : $"{characterOrByteCount / 1024.0:0.#}KB";
    }

    /// <summary>
    /// 输入规模描述重载：调用点直接传文本，长度在方法内部取。
    /// 不在调用点写 text?.Length ?? 0——对非空参数使用 ?. 判空会把它的可空流状态污染为"可能为 null"，
    /// 导致后续传给非空参数时产生 CS8604 警告。
    /// </summary>
    public static string DescribeSize(string? text)
    {
        return DescribeSize(text?.Length ?? 0);
    }
}
