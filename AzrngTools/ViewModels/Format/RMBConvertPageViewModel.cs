#nullable disable
using Azrng.Core.Helpers;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// 人民币金额大写转换
/// </summary>
public partial class RMBConvertPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public RMBConvertPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _amount = string.Empty;
        _uppercaseAmount = string.Empty;
    }

    #region 属性

    /// <summary>
    /// 金额
    /// </summary>
    [ObservableProperty]
    private string _amount;

    /// <summary>
    /// 大写金额
    /// </summary>
    [ObservableProperty]
    private string _uppercaseAmount;

    #endregion

    /// <summary>
    /// 转换为大写
    /// </summary>
    [RelayCommand]
    private void ConvertToUppercase()
    {
        try
        {
            if (Amount.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入金额");
                return;
            }

            UppercaseAmount = RmbHelper.ToRmbUpper(Amount.Trim());
            _messageService.SendMessage("转换成功");
        }
        catch (ArgumentException ex)
        {
            _messageService.SendMessage($"输入格式不正确：{ex.Message}");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清空
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Amount = string.Empty;
        UppercaseAmount = string.Empty;
    }

    /// <summary>
    /// 复制结果
    /// </summary>
    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (UppercaseAmount.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, UppercaseAmount);
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

    /// <summary>
    /// 设置示例金额
    /// </summary>
    [RelayCommand]
    private void SetExample()
    {
        Amount = "123456.78";
    }
}
