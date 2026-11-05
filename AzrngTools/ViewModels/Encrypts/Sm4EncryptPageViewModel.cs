#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using Common.Security;
using Common.Security.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AzrngTools.ViewModels.Encrypts;

public partial class Sm4EncryptPageViewModel : ViewModelBase
{
    private readonly ILogger<Sm4EncryptPageViewModel> _logger;
    private readonly IMessageService _messageService;

    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "encrypts-sm4";

    private readonly IPageInputStoreService _pageInputStore;

    // 构造期默认值赋值与恢复赋值都会触发 OnXxxChanged，置位跳过保存提交，避免默认值快照覆盖已有存档或恢复即回写
    private bool _isRestoring;

    public Sm4EncryptPageViewModel(ILogger<Sm4EncryptPageViewModel> logger, IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _isRestoring = true;
        _logger = logger;
        _messageService = messageService;
        _secret = "BMxA9xjQVLsJGEhD";
        OutTypeValue = (int)OutType.Hex;
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
    /// 密文
    /// </summary>
    [ObservableProperty]
    private string _ciphertext;

    /// <summary>
    /// 输出类型
    /// </summary>
    [ObservableProperty]
    private int _outTypeValue;

    #endregion

    [RelayCommand]
    private void Sm4Handler(string isEncrypt)
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要加密/解密的内容");
                return;
            }

            if (Secret.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入密钥");
                return;
            }

            using var perf = PerfLogScope.Start("SM4 加解密", $"输入 {PerfLogScope.DescribeSize(Original)}");
            var outType = OutTypeValue == 0 ? OutType.Base64 : OutType.Hex;
            Ciphertext = isEncrypt.Equals("true")
                ? Sm4Helper.Encrypt(Original, Secret, outType: outType)
                : Sm4Helper.Decrypt(Original, Secret, inputType: outType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"sm4加密失败  message:{ex.Message}");
            _messageService.SendMessage("加密/解密失败");
        }
    }
}