using Avalonia.Styling;
using AzrngTools.Services;
using Xunit;

namespace AzrngTools.Tests.Services;

// 主题偏好持久化回归：后台原子落盘后的读写闭环与损坏文件兜底
public class ThemePreferenceServiceTests
{
    private const string SettingsFileName = "theme-settings.json";

    [Fact]
    public void SaveThenLoad_RoundTripsThemeVariant()
    {
        var settingsDirectory = CreateTempDirectory();
        try
        {
            var service = new ThemePreferenceService(settingsDirectory);
            service.SaveRequestedThemeVariant(ThemeVariant.Dark);

            WaitForSettingsFile(settingsDirectory);

            // 新实例读回验证落盘内容；原子替换成功后不应残留临时文件
            var reloaded = new ThemePreferenceService(settingsDirectory);
            Assert.Equal(ThemeVariant.Dark, reloaded.LoadRequestedThemeVariant());
            Assert.False(File.Exists(Path.Combine(settingsDirectory, SettingsFileName + ".tmp")));
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    [Fact]
    public void Load_WithoutSettingsFile_ReturnsDefault()
    {
        var settingsDirectory = CreateTempDirectory();
        try
        {
            var service = new ThemePreferenceService(settingsDirectory);

            Assert.Equal(ThemeVariant.Default, service.LoadRequestedThemeVariant());
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    [Fact]
    public void Load_WithCorruptedSettingsFile_ReturnsDefault()
    {
        var settingsDirectory = CreateTempDirectory();
        try
        {
            // 模拟写中途崩溃留下的残缺文件：应兜底为默认主题而非崩溃
            File.WriteAllText(Path.Combine(settingsDirectory, SettingsFileName), "{ 不是 JSON");

            var service = new ThemePreferenceService(settingsDirectory);

            Assert.Equal(ThemeVariant.Default, service.LoadRequestedThemeVariant());
        }
        finally
        {
            if (Directory.Exists(settingsDirectory))
            {
                Directory.Delete(settingsDirectory, true);
            }
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AzrngTools.Tests", "theme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void WaitForSettingsFile(string settingsDirectory)
    {
        // 保存走后台线程落盘，轮询等待写入完成，避免测试对线程池时序敏感
        var settingsFile = Path.Combine(settingsDirectory, SettingsFileName);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(settingsFile) && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(50);
        }

        Assert.True(File.Exists(settingsFile), "主题设置文件未在超时时间内落盘。");
    }
}
