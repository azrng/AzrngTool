#nullable disable
using AzrngTools.Services;
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

    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "encode-hex";

    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public HexEncodePageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        RestoreInputs();
    }

    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            // Separator 由 SeparatorIndex 派生，只存索引即可还原
            [nameof(Original)] = PageInputValues.Encode(Original),
            [nameof(EncodeFormat)] = PageInputValues.Encode(EncodeFormat),
            [nameof(SeparatorIndex)] = PageInputValues.Encode(SeparatorIndex),
        });
    }

    private void RestoreInputs()
    {
        var saved = _pageInputStore.TryLoad(PageId);
        if (saved.Count == 0)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            if (saved.TryGetValue(nameof(Original), out var original))
            {
                Original = original;
            }

            if (saved.TryGetValue(nameof(EncodeFormat), out var encodeFormat))
            {
                EncodeFormat = PageInputValues.DecodeInt32(encodeFormat, EncodeFormat);
            }

            // SeparatorIndex 恢复会触发 OnSeparatorIndexChanged 派生出 Separator
            if (saved.TryGetValue(nameof(SeparatorIndex), out var separatorIndex))
            {
                SeparatorIndex = PageInputValues.DecodeInt32(separatorIndex, SeparatorIndex);
            }
        }
        finally
        {
            _isRestoring = false;
        }
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

    partial void OnOriginalChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnEncodeFormatChanged(int value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnSeparatorIndexChanged(int value)
    {
        // 恢复赋值期间只跳过存档提交，派生 Separator 的原逻辑仍要执行
        if (!_isRestoring)
        {
            SaveInputs();
        }

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
    private async Task StringToHexAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的内容");
                return;
            }

            var source = Original;
            var separator = Separator;
            // 大文本的编码转换移出 UI 线程，避免点击后界面冻结
            using var perf = PerfLogScope.Start("Hex 字符串编码", $"输入 {PerfLogScope.DescribeSize(source.Length)}");
            HandleText = await Task.Run(() => BytesToHex(Encoding.UTF8.GetBytes(source), separator));
        }
        catch (Exception ex)
        {
            AppLog.LogError($"字符串转十六进制失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 十六进制转字符串
    /// </summary>
    [RelayCommand]
    private async Task HexToStringAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的十六进制内容");
                return;
            }

            var hex = NormalizeHexInput(Original);
            if (hex.Length % 2 != 0)
            {
                _messageService.SendMessage("十六进制字符串长度必须为偶数");
                return;
            }

            using var perf = PerfLogScope.Start("Hex 字符串解码", $"输入 {PerfLogScope.DescribeSize(hex.Length)}");
            HandleText = await Task.Run(() => Encoding.UTF8.GetString(Convert.FromHexString(hex)));
        }
        catch (Exception ex)
        {
            AppLog.LogError($"十六进制转字符串失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 字节数组转十六进制
    /// </summary>
    [RelayCommand]
    private async Task BytesToHexAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的内容");
                return;
            }

            var source = Original;
            var separator = Separator;
            using var perf = PerfLogScope.Start("Hex 字节数组编码", $"输入 {PerfLogScope.DescribeSize(source.Length)}");
            HandleText = await Task.Run(() => BytesToHex(Encoding.UTF8.GetBytes(source), separator));
        }
        catch (Exception ex)
        {
            AppLog.LogError($"字节数组转十六进制失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 十六进制转字节数组
    /// </summary>
    [RelayCommand]
    private async Task HexToBytesAsync()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的十六进制内容");
                return;
            }

            var hex = NormalizeHexInput(Original);
            if (hex.Length % 2 != 0)
            {
                _messageService.SendMessage("十六进制字符串长度必须为偶数");
                return;
            }

            // 大十六进制串会生成超大逐字节文本，整体移到后台构建
            using var perf = PerfLogScope.Start("Hex 字节数组解码", $"输入 {PerfLogScope.DescribeSize(hex.Length)}");
            HandleText = await Task.Run(() =>
            {
                var bytes = Convert.FromHexString(hex);
                var sb = new StringBuilder($"字节数组长度：{bytes.Length}\r\n字节数组内容：\r\n", bytes.Length * 24 + 64);
                for (var i = 0; i < bytes.Length; i++)
                {
                    sb.AppendLine($"bytes[{i}] = 0x{bytes[i]:X2} ({bytes[i]})");
                }

                return sb.ToString();
            });
        }
        catch (Exception ex)
        {
            AppLog.LogError($"十六进制转字节数组失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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

            // SetTextAsync 回报真实写入结果：剪贴板不可用/被占用时不再提示"已复制"
            if (await ClipboardHelper.SetTextAsync(GetTopLevel(), HandleText))
            {
                _messageService.SendMessage("已复制到剪贴板");
            }
            else
            {
                _messageService.SendMessage("复制失败：剪贴板不可用");
            }
        }
        catch (Exception ex)
        {
            AppLog.LogError($"复制结果失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
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

    /// <summary>
    /// 移除十六进制输入中的空格、连字符、0x 前缀与换行
    /// </summary>
    private static string NormalizeHexInput(string input)
    {
        return input.Replace(" ", "").Replace("-", "").Replace("0x", "").Replace("0X", "").Replace("\r", "").Replace("\n", "");
    }
}
