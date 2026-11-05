#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AzrngTools.ViewModels.Encrypts;

/// <summary>
/// Hmac Hash处理
/// </summary>
public partial class HmacHashPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;
    private readonly ILogger<HmacHashPageViewModel> _logger;

    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "encrypts-hmac";

    private readonly IPageInputStoreService _pageInputStore;

    // 构造期默认值赋值与恢复赋值都会触发 OnXxxChanged，置位跳过保存提交，避免默认值快照覆盖已有存档或恢复即回写
    private bool _isRestoring;

    public HmacHashPageViewModel(IMessageService messageService, ILogger<HmacHashPageViewModel> logger, IPageInputStoreService pageInputStore)
    {
        _isRestoring = true;
        _messageService = messageService;
        _logger = logger;

        OutTypeValue = (int)OutType.Hex;
        Secret = Guid.NewGuid().ToString("N");
        _pageInputStore = pageInputStore;
        RestoreInputs();
        _isRestoring = false;
    }

    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            // Secret 属敏感键，绝不写入存档
            [nameof(Original)] = PageInputValues.Encode(Original),
            [nameof(OutTypeValue)] = PageInputValues.Encode(OutTypeValue),
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

            if (saved.TryGetValue(nameof(OutTypeValue), out var outTypeValue))
            {
                OutTypeValue = PageInputValues.DecodeInt32(outTypeValue, OutTypeValue);
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    partial void OnOriginalChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnOutTypeValueChanged(int value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    #region 属性

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _original;

    /// <summary>
    /// 密钥
    /// </summary>
    [ObservableProperty]
    private string _secret;

    /// <summary>
    /// 原文
    /// </summary>
    [ObservableProperty]
    private string _ciphertext;

    /// <summary>
    /// 输出类型
    /// </summary>
    [ObservableProperty]
    private int _outTypeValue;

    #endregion

    /// <summary>
    /// hmac 哈希处理
    /// </summary>
    [RelayCommand]
    private void Handler(string param)
    {
        if (Original.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("原始内容不能为空");
            return;
        }

        if (Secret.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("密钥不能为空");
            return;
        }

        try
        {
            using var perf = PerfLogScope.Start("HMAC 计算", $"输入 {PerfLogScope.DescribeSize(Original)}");
            Ciphertext = param switch
            {
                "0" => Md5Helper.GetHmacMd5Hash(Original, Secret, (OutType)OutTypeValue),
                "1" => ShaHelper.GetHmacSha1Hash(Original, Secret, (OutType)OutTypeValue),
                "2" => ShaHelper.GetHmacSha256Hash(Original, Secret, (OutType)OutTypeValue),
                "3" => ShaHelper.GetHmacSha512Hash(Original, Secret, (OutType)OutTypeValue),
                _ => Ciphertext
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"处理失败：{ex.Message}");
            _messageService.SendMessage($"处理失败：{ex.Message}");
        }
    }
}