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

    public ThemePreferenceService()
    {
        var appDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools");

        Directory.CreateDirectory(appDataDirectory);
        _settingsFilePath = Path.Combine(appDataDirectory, ThemeSettingsFileName);
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

        File.WriteAllText(_settingsFilePath, json);
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
