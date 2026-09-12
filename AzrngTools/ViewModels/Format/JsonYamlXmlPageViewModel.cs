using AzrngTools.Models.Format;
using AzrngTools.Services.Format;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// JSON / YAML / XML 互转工作台：负责格式选择、转换编排与校验结果展示。
/// </summary>
public partial class JsonYamlXmlPageViewModel : ViewModelBase
{
    private readonly IFormatConversionService _conversionService;
    private readonly IMessageService _messageService;

    public JsonYamlXmlPageViewModel(IFormatConversionService conversionService, IMessageService messageService)
    {
        _conversionService = conversionService;
        _messageService = messageService;
    }

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _targetText = string.Empty;

    [ObservableProperty]
    private FormatLanguage _sourceFormat = FormatLanguage.Json;

    [ObservableProperty]
    private FormatLanguage _targetFormat = FormatLanguage.Yaml;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// 是否为空输入（驱动视图侧转换按钮的可用态之外的信息展示，保留给状态区使用）
    /// </summary>
    [ObservableProperty]
    private bool _isValidationSuccess;

    /// <summary>
    /// 是否有可展示的状态消息：驱动贴底状态条显隐，空闲时不占布局空间
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    [RelayCommand]
    private void Convert()
    {
        if (SourceText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("请输入要转换的内容");
            return;
        }

        if (SourceFormat == TargetFormat)
        {
            SetResult(false, "源格式与目标格式一致，无需转换，可使用“格式化”。");
            return;
        }

        try
        {
            TargetText = _conversionService.Convert(SourceText, SourceFormat, TargetFormat);
            SetResult(true, $"转换成功：{SourceFormat} → {TargetFormat}。");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            SetResult(false, ex.Message);
            _messageService.SendMessage("转换失败，请检查输入内容");
        }
    }

    [RelayCommand]
    private void FormatSource()
    {
        if (SourceText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("请输入要格式化的内容");
            return;
        }

        try
        {
            SourceText = _conversionService.Format(SourceText, SourceFormat);
            SetResult(true, $"{SourceFormat} 格式化完成，结果已回填到输入区。");
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            SetResult(false, ex.Message);
            _messageService.SendMessage("格式化失败，请检查输入内容");
        }
    }

    [RelayCommand]
    private void Validate()
    {
        if (SourceText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("请输入要校验的内容");
            return;
        }

        var result = _conversionService.Validate(SourceText, SourceFormat);
        SetResult(result.IsSuccess, result.Message);
    }

    [RelayCommand]
    private void Swap()
    {
        (SourceFormat, TargetFormat) = (TargetFormat, SourceFormat);
        (SourceText, TargetText) = (TargetText, SourceText);
        SetResult(true, "已交换输入与输出，可反向转换。");
    }

    [RelayCommand]
    private void Clear()
    {
        SourceText = string.Empty;
        TargetText = string.Empty;
        SetResult(true, "已清空输入与输出。");
    }

    private void SetResult(bool isSuccess, string message)
    {
        IsValidationSuccess = isSuccess;
        ValidationMessage = message;
    }
}
