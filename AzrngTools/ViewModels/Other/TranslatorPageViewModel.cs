#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Other;

/// <summary>
/// 转换内容
/// </summary>
public partial class TranslatorPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "other-translator";

    private readonly ITranslationService _translationService;
    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public TranslatorPageViewModel(ITranslationService translationService, IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _translationService = translationService;
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        // 构造初始化赋值不属于用户输入，置位跳过保存提交，避免默认值快照在防抖落盘时覆盖已有存档
        _isRestoring = true;
        TranslatorTypeIndex = 0;
        _isRestoring = false;
        RestoreInputs();
    }

    /// <summary>
    /// 原始内容
    /// </summary>
    [ObservableProperty]
    private string _originText;

    /// <summary>
    /// 结果
    /// </summary>
    [ObservableProperty]
    private string _resultText;

    /// <summary>
    /// 转换类型索引
    /// </summary>
    [ObservableProperty]
    private int _translatorTypeIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    public bool CanTranslate => !IsBusy && !string.IsNullOrWhiteSpace(OriginText);

    [RelayCommand(CanExecute = nameof(CanExecuteTranslation))]
    private async Task Handler()
    {
        if (string.IsNullOrWhiteSpace(OriginText))
        {
            _messageService.SendMessage("请输入要转换的内容");
            return;
        }

        IsBusy = true;
        BusyText = "正在翻译...";
        try
        {
            using var perf = PerfLogScope.Start("翻译请求", $"输入 {PerfLogScope.DescribeSize(OriginText)}");
            switch (TranslatorTypeIndex)
            {
                case 0:
                    ResultText = await _translationService.YandexChineseToEnglishAsync(OriginText);
                    break;
                case 1:
                    ResultText = await _translationService.YandexEnglishToChineseAsync(OriginText);
                    break;
                default:
                    _messageService.SendMessage("无效的操作");
                    break;
            }
        }
        catch (Exception e)
        {
            AppLog.LogError($"翻译处理失败: {e.Message}\n{e.GetExceptionAndStack()}");
            _messageService.SendMessage($"处理失败：{e.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    private bool CanExecuteTranslation()
    {
        return CanTranslate;
    }

    partial void OnOriginTextChanged(string value)
    {
        // 恢复期间保留下方命令可用态通知，仅跳过保存提交避免恢复即回写
        if (!_isRestoring)
        {
            SaveInputs();
        }

        OnPropertyChanged(nameof(CanTranslate));
        HandlerCommand.NotifyCanExecuteChanged();
    }

    partial void OnTranslatorTypeIndexChanged(int value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            [nameof(OriginText)] = PageInputValues.Encode(OriginText),
            [nameof(TranslatorTypeIndex)] = PageInputValues.Encode(TranslatorTypeIndex),
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
            if (saved.TryGetValue(nameof(OriginText), out var originText))
            {
                OriginText = originText;
            }

            if (saved.TryGetValue(nameof(TranslatorTypeIndex), out var translatorTypeIndex))
            {
                TranslatorTypeIndex = PageInputValues.DecodeInt32(translatorTypeIndex, TranslatorTypeIndex);
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTranslate));
        HandlerCommand.NotifyCanExecuteChanged();
    }
}
