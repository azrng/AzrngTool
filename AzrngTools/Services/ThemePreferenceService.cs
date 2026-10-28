using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Styling;

namespace AzrngTools.Services;

public sealed partial class ThemePreferenceService : IThemePreferenceService
{
    private const string ThemeSettingsFileName = "theme-settings.json";
    private readonly string _settingsFilePath;
    private readonly object _writeLock = new();

    public ThemePreferenceService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools"))
    {
    }

    // 供测试注入独立目录，避免读写真实用户配置
    internal ThemePreferenceService(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsFilePath = Path.Combine(settingsDirectory, ThemeSettingsFileName);
    }

    public ThemeVariant LoadRequestedThemeVariant()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return ThemeVariant.Default;
            }

            var raw = File.ReadAllText(_settingsFilePath).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return ThemeVariant.Default;
            }

            var settings = JsonSerializer.Deserialize(raw, AppThemeJsonContext.Default.ThemePreferenceSettings);
            return settings?.RequestedThemeVariant?.Trim().ToLowerInvariant() switch
            {
                "light" => ThemeVariant.Light,
                "dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"加载主题偏好失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            return ThemeVariant.Default;
        }
    }

    public void SaveRequestedThemeVariant(ThemeVariant themeVariant)
    {
        var value = themeVariant == ThemeVariant.Dark
            ? "Dark"
            : themeVariant == ThemeVariant.Light
                ? "Light"
                : "Default";

        var settings = new ThemePreferenceSettings
        {
            RequestedThemeVariant = value
        };

        var json = JsonSerializer.Serialize(settings, AppThemeJsonContext.Default.ThemePreferenceSettings);

        // 主题切换事件在 UI 线程触发，写盘挪到线程池避免界面卡顿；快速连续切换时各次写入
        // 由锁串行化，最终落盘内容与最后一次切换存在极小概率乱序，下次切换即自愈，
        // 不值得为此引入排队机制
        Task.Run(() => WriteSettingsAtomically(json));
    }

    private void WriteSettingsAtomically(string json)
    {
        try
        {
            lock (_writeLock)
            {
                // 先写临时文件再原子替换，写中途崩溃不会损坏已有设置（与项目其余持久化服务一致）
                var tempFilePath = _settingsFilePath + ".tmp";
                File.WriteAllText(tempFilePath, json);
                File.Move(tempFilePath, _settingsFilePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"保存主题偏好失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
        }
    }

    private sealed class ThemePreferenceSettings
    {
        public string RequestedThemeVariant { get; set; } = "Default";
    }

    // 源生成 JSON 上下文：AOT 发布下不能用反射序列化
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ThemePreferenceSettings))]
    private sealed partial class AppThemeJsonContext : JsonSerializerContext
    {
    }
}
