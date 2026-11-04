using System.Text.RegularExpressions;
using AzrngTools.Utils;

namespace AzrngTools.Tests.Utils;

/// <summary>
/// AppLog 本地日志回归：按天文件落盘格式、error.log 双写、目录懒创建、批量不丢不重、30 天过期清理。
/// 全部通过 ConfigureForTests 指向临时目录 + FlushPendingForTests 同步排空，保证断言确定性；
/// Collection 串行化避免日志器静态状态被并行测试串扰。
/// </summary>
[Collection("AppLog")]
public class AppLogTests
{
    /// <summary>行格式：yyyy-MM-dd HH:mm:ss.fff [type] info。</summary>
    private static readonly Regex DailyLinePattern = new(
        @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} \[测试\] .*$", RegexOptions.Compiled);

    [Fact]
    public void WriteMyLogs_Flushed_WritesDailyFileWithExpectedFormat()
    {
        var logDirectory = CreateTempLogDirectory();
        try
        {
            AppLog.ConfigureForTests(logDirectory);
            AppLog.WriteMyLogs("测试", "格式校验条目");
            AppLog.FlushPendingForTests();

            var dailyFilePath = Path.Combine(logDirectory, DateTime.Now.ToString("yyyyMMdd") + ".log");
            Assert.True(File.Exists(dailyFilePath), $"按天日志文件应存在：{dailyFilePath}");

            // 并行的性能探针测试可能向同一日志器入队条目，按本测试唯一标记过滤后再断言
            var matchedLines = File.ReadAllLines(dailyFilePath)
                .Where(line => line.Contains("[测试] 格式校验条目", StringComparison.Ordinal))
                .ToArray();
            var line = Assert.Single(matchedLines);
            Assert.Matches(DailyLinePattern, line);
            Assert.EndsWith("[测试] 格式校验条目", line);
        }
        finally
        {
            DeleteDirectoryQuietly(logDirectory);
        }
    }

    [Fact]
    public void LogError_Flushed_WritesErrorLogAndDailyFile()
    {
        var logDirectory = CreateTempLogDirectory();
        try
        {
            AppLog.ConfigureForTests(logDirectory);
            AppLog.LogError("错误条目内容");
            AppLog.FlushPendingForTests();

            var errorFilePath = Path.Combine(logDirectory, "error.log");
            Assert.True(File.Exists(errorFilePath), "error.log 应存在");

            var errorContent = File.ReadAllText(errorFilePath);
            Assert.Contains("[错误] 错误条目内容", errorContent);

            var dailyFilePath = Path.Combine(logDirectory, DateTime.Now.ToString("yyyyMMdd") + ".log");
            Assert.True(File.Exists(dailyFilePath), "当日日志文件应存在");
            Assert.Contains("[错误] 错误条目内容", File.ReadAllText(dailyFilePath));
        }
        finally
        {
            DeleteDirectoryQuietly(logDirectory);
        }
    }

    [Fact]
    public void ConfigureForTests_WithMissingDirectory_LazilyCreatedOnWrite()
    {
        var logDirectory = Path.Combine(CreateTempLogDirectory(), "not-exist", "logs");
        try
        {
            // 目录及父目录都不存在，写入后应懒创建成功
            AppLog.ConfigureForTests(logDirectory);
            AppLog.WriteMyLogs("测试", "懒创建条目");
            AppLog.FlushPendingForTests();

            var dailyFilePath = Path.Combine(logDirectory, DateTime.Now.ToString("yyyyMMdd") + ".log");
            Assert.True(File.Exists(dailyFilePath), "懒创建目录后按天文件应存在");
            Assert.Contains("懒创建条目", File.ReadAllText(dailyFilePath));
        }
        finally
        {
            // 清理最外层临时目录即可连同子目录一起删除
            DeleteDirectoryQuietly(Path.GetFullPath(Path.Combine(logDirectory, "..", "..")));
        }
    }

    [Fact]
    public void WriteManyEntries_Flushed_AllPresentExactlyOnce()
    {
        var logDirectory = CreateTempLogDirectory();
        try
        {
            AppLog.ConfigureForTests(logDirectory);

            const int entryCount = 200;
            for (var index = 0; index < entryCount; index++)
            {
                AppLog.WriteMyLogs("测试", $"批量条目-{index}");
            }

            AppLog.FlushPendingForTests();

            var dailyFilePath = Path.Combine(logDirectory, DateTime.Now.ToString("yyyyMMdd") + ".log");

            // 并行的性能探针测试可能向同一日志器入队条目，按本测试唯一标记过滤后再断言不丢不重
            var lines = File.ReadAllLines(dailyFilePath)
                .Where(line => line.Contains("[测试] 批量条目-", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(entryCount, lines.Length);

            foreach (var line in lines)
            {
                Assert.Matches(DailyLinePattern, line);
            }

            for (var index = 0; index < entryCount; index++)
            {
                var expectedSuffix = $"[测试] 批量条目-{index}";
                Assert.Equal(1, lines.Count(line => line.EndsWith(expectedSuffix, StringComparison.Ordinal)));
            }
        }
        finally
        {
            DeleteDirectoryQuietly(logDirectory);
        }
    }

    [Fact]
    public void FlushPendingForTests_RemovesFilesOlderThanRetention_KeepsTodayFile()
    {
        var logDirectory = CreateTempLogDirectory();
        try
        {
            // 先建目录再伪造 40 天前的过期日志文件（含 error.log）
            Directory.CreateDirectory(logDirectory);
            var staleFilePath = Path.Combine(logDirectory, "20200101.log");
            File.WriteAllText(staleFilePath, "过期日志");
            File.SetLastWriteTime(staleFilePath, DateTime.Now.AddDays(-40));
            var staleErrorFilePath = Path.Combine(logDirectory, "error.log");
            File.WriteAllText(staleErrorFilePath, "过期错误日志");
            File.SetLastWriteTime(staleErrorFilePath, DateTime.Now.AddDays(-40));

            AppLog.ConfigureForTests(logDirectory);
            AppLog.WriteMyLogs("测试", "今日条目");
            AppLog.FlushPendingForTests();

            Assert.False(File.Exists(staleFilePath), "超过 30 天的 .log 应被清理");
            Assert.False(File.Exists(staleErrorFilePath), "超过 30 天的 error.log 应被清理");

            var dailyFilePath = Path.Combine(logDirectory, DateTime.Now.ToString("yyyyMMdd") + ".log");
            Assert.True(File.Exists(dailyFilePath), "当日日志文件应保留");
            Assert.Contains("今日条目", File.ReadAllText(dailyFilePath));
        }
        finally
        {
            DeleteDirectoryQuietly(logDirectory);
        }
    }

    /// <summary>临时目录：%TEMP%\AzrngTools.Tests\applog-{guid}，与其他测试目录隔离。</summary>
    private static string CreateTempLogDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "applog-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>尽力清理临时目录，不因清理失败影响测试结果。</summary>
    private static void DeleteDirectoryQuietly(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
