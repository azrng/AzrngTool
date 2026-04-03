using System;
using System.IO;
using System.Text.Json;
using Avalonia.Styling;

namespace AzrngTools.Services;

public sealed class ThemePreferenceService : IThemePreferenceService
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

            var settings = JsonSerializer.Deserialize<ThemePreferenceSettings>(raw);
            return settings?.RequestedThemeVariant?.Trim().ToLowerInvariant() switch
            {
                "light" => ThemeVariant.Light,
                "dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
        catch
        {
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

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_settingsFilePath, json);
    }

    private sealed class ThemePreferenceSettings
    {
        public string RequestedThemeVariant { get; set; } = "Default";
    }
}
