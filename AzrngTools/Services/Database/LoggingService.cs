using System;
using System.IO;
using System.Threading.Tasks;

namespace AzrngTools.Services.Database;

/// <summary>
/// 简单的日志服务
/// </summary>
public static class LoggingService
{
    private static readonly string LogDirectory = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "logs");

    private static readonly string LogFile = Path.Combine(
        LogDirectory,
        $"app_{DateTime.Now:yyyyMMdd}.log");

    private static readonly object LockObject = new();

    /// <summary>
    /// 初始化日志服务
    /// </summary>
    public static void Initialize()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化日志目录失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 记录信息日志
    /// </summary>
    public static void LogInfo(string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        WriteLog("INFO", message, caller, null);
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    public static void LogWarning(string message, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        WriteLog("WARNING", message, caller, null);
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    public static void LogError(string message, Exception? exception = null, [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        WriteLog("ERROR", message, caller, exception);
    }

    /// <summary>
    /// 记录操作日志
    /// </summary>
    public static void LogOperation(string operation, string details = "")
    {
        var message = string.IsNullOrWhiteSpace(details)
            ? $"操作：{operation}"
            : $"操作：{operation} - {details}";
        WriteLog("OPERATION", message, "Operation", null);
    }

    /// <summary>
    /// 写入日志
    /// </summary>
    private static void WriteLog(string level, string message, string caller, Exception? exception)
    {
        Task.Run(() =>
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var exceptionInfo = exception != null
                    ? $"\n  异常类型：{exception.GetType().Name}\n  异常消息：{exception.Message}\n  堆栈跟踪：{exception.StackTrace}"
                    : "";

                var logEntry = $"[{timestamp}] [{level}] [{caller}] {message}{exceptionInfo}";

                lock (LockObject)
                {
                    File.AppendAllText(LogFile, logEntry + Environment.NewLine);
                }

                // 同时输出到调试控制台
                System.Diagnostics.Debug.WriteLine(logEntry);
            }
            catch (Exception ex)
            {
                // 日志写入失败时只输出到调试控制台
                System.Diagnostics.Debug.WriteLine($"写入日志失败：{ex.Message}");
            }
        });
    }

    /// <summary>
    /// 获取最近的错误日志
    /// </summary>
    public static string[] GetRecentErrors(int count = 10)
    {
        try
        {
            if (!File.Exists(LogFile))
            {
                return Array.Empty<string>();
            }

            var allLines = File.ReadAllLines(LogFile);
            var errorLines = allLines
                .Where(line => line.Contains("[ERROR]"))
                .TakeLast(count)
                .ToArray();

            return errorLines;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取错误日志失败：{ex.Message}");
            return Array.Empty<string>();
        }
    }
}
