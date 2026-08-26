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
    private readonly ITranslationService _translationService;
    private readonly IMessageService _messageService;

    public TranslatorPageViewModel(ITranslationService translationService, IMessageService messageService)
    {
        TranslatorTypeIndex = 0;
        _translationService = translationService;
        _messageService = messageService;
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
            LocalLogHelper.LogError($"翻译处理失败: {e.Message}\n{e.GetExceptionAndStack()}");
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
        OnPropertyChanged(nameof(CanTranslate));
        HandlerCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanTranslate));
        HandlerCommand.NotifyCanExecuteChanged();
    }
}
