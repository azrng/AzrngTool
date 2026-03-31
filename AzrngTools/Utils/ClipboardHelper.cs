using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace AzrngTools.Utils;

/// <summary>
/// Clipboard helper methods.
/// </summary>
public static class ClipboardHelper
{
    public static async Task SetTextAsync(TopLevel topLevel, string text)
    {
        if (topLevel?.Clipboard is not null)
        {
            await topLevel.Clipboard.SetTextAsync(text);
        }
    }

    public static async Task<string> GetTextAsync(TopLevel topLevel)
    {
        if (topLevel?.Clipboard is not null)
        {
            return await topLevel.Clipboard.TryGetTextAsync() ?? string.Empty;
        }

        return string.Empty;
    }
}
