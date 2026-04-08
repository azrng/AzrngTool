#nullable disable
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using Azrng.Core.Extension;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Web;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// URL编码/解码
/// </summary>
public partial class UrlEncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public UrlEncodePageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    #region 属性

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _original;

    /// <summary>
    /// 处理后的文本
    /// </summary>
    [ObservableProperty]
    private string _handleText;

    #endregion

    /// <summary>
    /// URL编码
    /// </summary>
    [RelayCommand]
    private void UrlEncode()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要编码的内容");
                return;
            }

            HandleText = HttpUtility.UrlEncode(Original);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"编码失败：{ex.Message}");
        }
    }

    /// <summary>
    /// URL解码
    /// </summary>
    [RelayCommand]
    private void UrlDecode()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要解码的内容");
                return;
            }

            HandleText = HttpUtility.UrlDecode(Original);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"解码失败：{ex.Message}");
        }
    }

    /// <summary>
    /// URL组件编码（不编码特殊字符如 / ? : 等）
    /// </summary>
    [RelayCommand]
    private void UrlPathComponentEncode()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要编码的内容");
                return;
            }

            HandleText = Original.UrlEncode();
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"编码失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清空
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Original = string.Empty;
        HandleText = string.Empty;
    }

    /// <summary>
    /// 复制结果
    /// </summary>
    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (HandleText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, HandleText);
            }
            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取 TopLevel
    /// </summary>
    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
