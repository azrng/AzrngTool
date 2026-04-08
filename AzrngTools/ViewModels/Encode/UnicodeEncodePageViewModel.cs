#nullable disable
using Azrng.Core.Helpers;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// Unicode编码/解码
/// </summary>
public partial class UnicodeEncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public UnicodeEncodePageViewModel(IMessageService messageService)
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
    /// Unicode编码
    /// </summary>
    [RelayCommand]
    private void UnicodeEncode()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要编码的内容");
                return;
            }

            HandleText = StringHelper.TextToUnicode(Original);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"编码失败：{ex.Message}");
        }
    }

    /// <summary>
    /// Unicode解码
    /// </summary>
    [RelayCommand]
    private void UnicodeDecode()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要解码的内容");
                return;
            }

            HandleText = StringHelper.UnicodeToText(Original);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"解码失败：{ex.Message}");
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
