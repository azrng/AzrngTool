#nullable disable
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;

namespace AzrngTools.ViewModels.Encode;

/// <summary>
/// 十六进制转换
/// </summary>
public partial class HexEncodePageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public HexEncodePageViewModel(IMessageService messageService)
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

    /// <summary>
    /// 编码格式选择
    /// </summary>
    [ObservableProperty]
    private int _encodeFormat;

    /// <summary>
    /// 分隔符
    /// </summary>
    [ObservableProperty]
    private string _separator = " ";

    /// <summary>
    /// 分隔符索引
    /// </summary>
    [ObservableProperty]
    private int _separatorIndex;

    partial void OnSeparatorIndexChanged(int value)
    {
        Separator = value switch
        {
            0 => " ",
            1 => string.Empty,
            2 => "-",
            3 => ":",
            _ => " "
        };
    }

    #endregion

    /// <summary>
    /// 字符串转十六进制
    /// </summary>
    [RelayCommand]
    private void StringToHex()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的内容");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(Original);
            HandleText = BytesToHex(bytes, Separator);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 十六进制转字符串
    /// </summary>
    [RelayCommand]
    private void HexToString()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的十六进制内容");
                return;
            }

            // 移除所有空格和分隔符
            var hex = Original.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "").Replace("\r", "").Replace("\n", "");

            if (hex.Length % 2 != 0)
            {
                _messageService.SendMessage("十六进制字符串长度必须为偶数");
                return;
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var byteValue = hex.Substring(i * 2, 2);
                bytes[i] = Convert.ToByte(byteValue, 16);
            }

            HandleText = Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 字节数组转十六进制
    /// </summary>
    [RelayCommand]
    private void BytesToHex()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的内容");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(Original);
            HandleText = BytesToHex(bytes, Separator);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 十六进制转字节数组
    /// </summary>
    [RelayCommand]
    private void HexToBytes()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的十六进制内容");
                return;
            }

            // 移除所有空格和分隔符
            var hex = Original.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "").Replace("\r", "").Replace("\n", "");

            if (hex.Length % 2 != 0)
            {
                _messageService.SendMessage("十六进制字符串长度必须为偶数");
                return;
            }

            var bytes = new byte[hex.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                var byteValue = hex.Substring(i * 2, 2);
                bytes[i] = Convert.ToByte(byteValue, 16);
            }

            HandleText = $"字节数组长度：{bytes.Length}\r\n";
            HandleText += "字节数组内容：\r\n";
            for (var i = 0; i < bytes.Length; i++)
            {
                HandleText += $"bytes[{i}] = 0x{bytes[i]:X2} ({bytes[i]})\r\n";
            }
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

    /// <summary>
    /// 字节数组转十六进制字符串
    /// </summary>
    private string BytesToHex(byte[] bytes, string separator)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        var hex = new StringBuilder(bytes.Length * 2);
        for (var i = 0; i < bytes.Length; i++)
        {
            hex.Append(bytes[i].ToString("X2"));
            if (i < bytes.Length - 1 && !string.IsNullOrEmpty(separator))
            {
                hex.Append(separator);
            }
        }
        return hex.ToString();
    }
}
