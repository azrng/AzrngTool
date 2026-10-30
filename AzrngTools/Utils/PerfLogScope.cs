using System.Diagnostics;
using Azrng.Core.Helpers;

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
    private readonly long _thresholdMs;
    private readonly Stopwatch _stopwatch;

    private PerfLogScope(string operation, string? context, long thresholdMs)
    {
        _operation = operation;
        _context = context;
        _thresholdMs = thresholdMs;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// 开始计时。thresholdMs 传 0 表示无条件记录（用于启动、更新下载等诊断价值恒定的链路）。
    /// </summary>
    public static PerfLogScope Start(string operation, string? context = null, int thresholdMs = DefaultThresholdMs)
    {
        return new PerfLogScope(operation, context, thresholdMs);
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        if (_stopwatch.ElapsedMilliseconds < _thresholdMs)
        {
            return;
        }

        var context = _context is null ? string.Empty : $"（{_context}）";
        LocalLogHelper.WriteMyLogs("性能检测", $"{_operation} 耗时 {_stopwatch.ElapsedMilliseconds}ms{context}");
    }

    /// <summary>输入规模描述：文本字符数或文件字节数换算为可读 KB/MB，仅用于日志上下文。</summary>
    public static string DescribeSize(long characterOrByteCount)
    {
        return characterOrByteCount >= 1024 * 1024
            ? $"{characterOrByteCount / (1024.0 * 1024.0):0.#}MB"
            : $"{characterOrByteCount / 1024.0:0.#}KB";
    }
}
