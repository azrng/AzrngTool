using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace AzrngTools.Utils;

/// <summary>
/// Clipboard helper methods.
/// </summary>
public static class ClipboardHelper
{
    /// <summary>
    /// 写入剪贴板。返回 false 表示宿主剪贴板不可用或系统拒绝写入，调用方据此给出真实结果提示，
    /// 避免「未写入也提示已复制」的假成功。
    /// </summary>
    public static async Task<bool> SetTextAsync(TopLevel? topLevel, string text)
    {
        if (topLevel?.Clipboard is null)
        {
            return false;
        }

        try
        {
            // Avalonia 12 写侧无 Try API：剪贴板被其他进程占用等平台拒绝场景 SetTextAsync 抛异常而非返回失败标志
            await topLevel.Clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<string> GetTextAsync(TopLevel? topLevel)
    {
        if (topLevel?.Clipboard is not null)
        {
            return await topLevel.Clipboard.TryGetTextAsync() ?? string.Empty;
        }

        return string.Empty;
    }
}
