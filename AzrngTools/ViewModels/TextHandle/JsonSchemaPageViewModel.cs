#nullable disable
using AzrngTools.Services;
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
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "generation-json-schema";

    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnOriginTextChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public JsonSchemaPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        RestoreInputs();
    }

    partial void OnOriginTextChanged(string value)
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
        }
        finally
        {
            _isRestoring = false;
        }
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
            using (PerfLogScope.Start("JSON Schema生成", $"输入 {PerfLogScope.DescribeSize(source.Length)}"))
            {
                // NJsonSchema 基于反射、大样本明显耗时，移出 UI 线程避免点击后界面冻结
                var schemaStr = await Task.Run(() => JsonSchema.FromSampleJson(source).ToJson());
                SchemaData = JsonHelper.JsonFormatter(schemaStr);
            }
        }
        catch (Exception ex)
        {
            AppLog.WriteMyLogs("ERROR", $"生成json schema 出错：{ex.Message}");
            _messageService.SendMessage(ex.Message);
        }
    }
}