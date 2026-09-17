#nullable disable
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NJsonSchema;

namespace AzrngTools.ViewModels.TextHandle;

/// <summary>
/// json schema 生成
/// </summary>
public partial class JsonSchemaPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public JsonSchemaPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// 原始文本
    /// </summary>
    [ObservableProperty]
    private string _originText;

    /// <summary>
    /// schema数据
    /// </summary>
    [ObservableProperty]
    private string _schemaData;

    /// <summary>
    /// schema 生成
    /// </summary>
    [RelayCommand]
    private async Task SchemaGenHandleAsync()
    {
        if (OriginText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("请输出要生成Schema的Json");
            return;
        }

        try
        {
            var source = OriginText;
            // NJsonSchema 基于反射、大样本明显耗时，移出 UI 线程避免点击后界面冻结
            var schemaStr = await Task.Run(() => JsonSchema.FromSampleJson(source).ToJson());
            SchemaData = JsonHelper.JsonFormatter(schemaStr);
        }
        catch (Exception ex)
        {
            LocalLogHelper.WriteMyLogs("ERROR", $"生成json schema 出错：{ex.Message}");
            _messageService.SendMessage(ex.Message);
        }
    }
}