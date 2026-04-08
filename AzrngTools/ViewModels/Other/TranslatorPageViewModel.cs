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

    [RelayCommand]
    private async Task Handler()
    {
        if (OriginText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("请输入要转换的内容");
        }

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
            _messageService.SendMessage($"处理失败：{e.Message}");
        }
    }
}