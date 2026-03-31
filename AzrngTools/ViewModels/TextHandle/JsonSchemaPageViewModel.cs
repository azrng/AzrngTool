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
    private void SchemaGenHandle()
    {
        if (OriginText.IsNullOrWhiteSpace())
        {
            _messageService.SendMessage("请输出要生成Schema的Json");
            return;
        }

        try
        {
            var schema = JsonSchema.FromSampleJson(OriginText);

            // 输出schema
            var schemaStr = schema.ToJson();
            SchemaData = JsonHelper.JsonFormatter(schemaStr);
        }
        catch (Exception ex)
        {
            LocalLogHelper.WriteMyLogs("ERROR", $"生成json schema 出错：{ex.Message}");
            _messageService.SendMessage(ex.Message);
        }
    }
}