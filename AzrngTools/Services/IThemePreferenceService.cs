using Avalonia.Styling;

namespace AzrngTools.Services;

public interface IThemePreferenceService
{
    ThemeVariant LoadRequestedThemeVariant();

    void SaveRequestedThemeVariant(ThemeVariant themeVariant);
}
