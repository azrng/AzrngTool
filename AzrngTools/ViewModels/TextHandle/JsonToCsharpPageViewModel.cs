#nullable disable
using AzrngTools.Services;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzrngTools.ViewModels.TextHandle;

public partial class JsonToCsharpPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "generation-json-to-csharp";

    private readonly IMessageService _messageService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public JsonToCsharpPageViewModel(IMessageService messageService, IPageInputStoreService pageInputStore)
    {
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        _rootClassName = "RootObject";
        _nameSpace = "MyApplication.Models";
        _useNullableTypes = true;
        _usePascalCase = true;
        _generateProperties = true;
        RestoreInputs();
    }

    partial void OnJsonInputChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnRootClassNameChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnNameSpaceChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnUseNullableTypesChanged(bool value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnUsePascalCaseChanged(bool value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnGeneratePropertiesChanged(bool value)
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
            [nameof(JsonInput)] = PageInputValues.Encode(JsonInput),
            [nameof(RootClassName)] = PageInputValues.Encode(RootClassName),
            [nameof(NameSpace)] = PageInputValues.Encode(NameSpace),
            [nameof(UseNullableTypes)] = PageInputValues.Encode(UseNullableTypes),
            [nameof(UsePascalCase)] = PageInputValues.Encode(UsePascalCase),
            [nameof(GenerateProperties)] = PageInputValues.Encode(GenerateProperties),
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
            if (saved.TryGetValue(nameof(JsonInput), out var jsonInput))
            {
                JsonInput = jsonInput;
            }

            if (saved.TryGetValue(nameof(RootClassName), out var rootClassName))
            {
                RootClassName = rootClassName;
            }

            if (saved.TryGetValue(nameof(NameSpace), out var nameSpace))
            {
                NameSpace = nameSpace;
            }

            if (saved.TryGetValue(nameof(UseNullableTypes), out var useNullableTypes))
            {
                UseNullableTypes = PageInputValues.DecodeBool(useNullableTypes, UseNullableTypes);
            }

            if (saved.TryGetValue(nameof(UsePascalCase), out var usePascalCase))
            {
                UsePascalCase = PageInputValues.DecodeBool(usePascalCase, UsePascalCase);
            }

            if (saved.TryGetValue(nameof(GenerateProperties), out var generateProperties))
            {
                GenerateProperties = PageInputValues.DecodeBool(generateProperties, GenerateProperties);
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    [ObservableProperty]
    private string _jsonInput = string.Empty;

    [ObservableProperty]
    private string _csharpOutput = string.Empty;

    [ObservableProperty]
    private string _rootClassName;

    [ObservableProperty]
    private string _nameSpace;

    [ObservableProperty]
    private bool _useNullableTypes;

    [ObservableProperty]
    private bool _usePascalCase;

    [ObservableProperty]
    private bool _generateProperties;

    [RelayCommand]
    private async Task ConvertToCsharpAsync()
    {
        try
        {
            if (JsonInput.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入 JSON 内容");
                return;
            }

            var source = JsonInput;
            // 大 JSON 的解析与类生成可能耗时明显，移出 UI 线程避免点击后界面冻结
            using var perf = PerfLogScope.Start("JSON 转 C#", $"输入 {PerfLogScope.DescribeSize(source.Length)}");
            var result = await Task.Run(() => BuildCsharpCode(
                source,
                new GenerationOptions
                {
                    UseNullableTypes = UseNullableTypes,
                    UsePascalCase = UsePascalCase,
                    GenerateProperties = GenerateProperties,
                    NameSpace = NameSpace
                },
                NormalizeClassName(RootClassName, "RootObject", UsePascalCase)));

            if (!result.RootIsObject)
            {
                _messageService.SendMessage("JSON 根节点必须是对象");
                return;
            }

            CsharpOutput = result.Code;
            _messageService.SendMessage($"转换成功，生成了 {result.ClassCount} 个类");
        }
        catch (JsonException ex)
        {
            AppLog.LogError($"JSON 转 C# 解析失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"JSON 解析失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"JSON 转 C# 转换失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        JsonInput = string.Empty;
        CsharpOutput = string.Empty;
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (CsharpOutput.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel?.Clipboard is not null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, CsharpOutput);
            }

            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"复制 C# 转换结果失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task FormatJsonAsync()
    {
        try
        {
            if (JsonInput.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入 JSON 内容");
                return;
            }

            var source = JsonInput;
            using var perf = PerfLogScope.Start("JSON 格式化（转C#页）", $"输入 {PerfLogScope.DescribeSize(source.Length)}");
            JsonInput = await Task.Run(() =>
            {
                using var jsonDoc = JsonDocument.Parse(source);
                return JsonHelper.FormatJsonDocument(jsonDoc);
            });
        }
        catch (JsonException ex)
        {
            AppLog.LogError($"JSON 格式化解析失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"JSON 格式化失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            AppLog.LogError($"JSON 格式化失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            _messageService.SendMessage($"格式化失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 生成选项快照：转换在后台线程执行，选项在点击时刻定格
    /// </summary>
    private sealed class GenerationOptions
    {
        public bool UseNullableTypes { get; init; }
        public bool UsePascalCase { get; init; }
        public bool GenerateProperties { get; init; }
        public string NameSpace { get; init; } = string.Empty;
    }

    private sealed record GenerationResult(string? Code, int ClassCount, bool RootIsObject);

    private static GenerationResult BuildCsharpCode(string jsonInput, GenerationOptions options, string rootClassName)
    {
        using var jsonDoc = JsonDocument.Parse(jsonInput);
        if (jsonDoc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new GenerationResult(null, 0, false);
        }

        var generatedClasses = new HashSet<string>(StringComparer.Ordinal);
        var classDefinitions = new List<string>();
        GenerateClass(jsonDoc.RootElement, rootClassName, options, classDefinitions, generatedClasses);

        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine($"namespace {options.NameSpace}");
        builder.AppendLine("{");

        for (var i = 0; i < classDefinitions.Count; i++)
        {
            builder.Append(classDefinitions[i]);
            if (i < classDefinitions.Count - 1)
            {
                builder.AppendLine();
            }
        }

        builder.AppendLine("}");
        return new GenerationResult(builder.ToString(), generatedClasses.Count, true);
    }

    private static void GenerateClass(JsonElement element, string className, GenerationOptions options, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        if (!generatedClasses.Add(className))
        {
            return;
        }

        var builder = new StringBuilder();
        const string indent = "    ";
        const string propertyIndent = "        ";

        builder.AppendLine($"{indent}public class {className}");
        builder.AppendLine($"{indent}{{");

        foreach (var property in element.EnumerateObject())
        {
            var memberName = NormalizeMemberName(property.Name, options.UsePascalCase);
            var suggestedClassName = NormalizeClassName(memberName, "AnonymousObject", options.UsePascalCase);
            var propertyType = ResolveType(property.Value, suggestedClassName, options, classDefinitions, generatedClasses);

            if (options.GenerateProperties)
            {
                builder.AppendLine($"{propertyIndent}public {propertyType} {memberName} {{ get; set; }}");
            }
            else
            {
                builder.AppendLine($"{propertyIndent}public {propertyType} {memberName};");
            }
        }

        builder.AppendLine($"{indent}}}");
        classDefinitions.Add(builder.ToString());
    }

    private static string ResolveType(JsonElement element, string suggestedClassName, GenerationOptions options, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => ResolveNumberType(element, options),
            JsonValueKind.True => ResolveNullableValueType("bool", options),
            JsonValueKind.False => ResolveNullableValueType("bool", options),
            JsonValueKind.Null => "object",
            JsonValueKind.Object => ResolveObjectType(element, suggestedClassName, options, classDefinitions, generatedClasses),
            JsonValueKind.Array => ResolveArrayType(element, suggestedClassName, options, classDefinitions, generatedClasses),
            _ => "object"
        };
    }

    private static string ResolveObjectType(JsonElement element, string suggestedClassName, GenerationOptions options, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        GenerateClass(element, suggestedClassName, options, classDefinitions, generatedClasses);
        return suggestedClassName;
    }

    private static string ResolveArrayType(JsonElement element, string suggestedClassName, GenerationOptions options, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null || item.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            var itemType = item.ValueKind == JsonValueKind.Object
                ? ResolveObjectType(item, $"{suggestedClassName}Item", options, classDefinitions, generatedClasses)
                : ResolveType(item, $"{suggestedClassName}Item", options, classDefinitions, generatedClasses);

            return $"List<{itemType}>";
        }

        return "List<object>";
    }

    private static string ResolveNumberType(JsonElement element, GenerationOptions options)
    {
        if (element.TryGetInt32(out _))
        {
            return ResolveNullableValueType("int", options);
        }

        if (element.TryGetInt64(out _))
        {
            return ResolveNullableValueType("long", options);
        }

        if (element.TryGetDecimal(out _))
        {
            return ResolveNullableValueType("decimal", options);
        }

        return ResolveNullableValueType("double", options);
    }

    private static string ResolveNullableValueType(string typeName, GenerationOptions options)
    {
        return options.UseNullableTypes ? $"{typeName}?" : typeName;
    }

    private static string NormalizeMemberName(string text, bool usePascalCase)
    {
        var normalized = usePascalCase ? ToPascalCase(text) : SanitizeIdentifier(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Property";
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = $"_{normalized}";
        }

        return normalized;
    }

    private static string NormalizeClassName(string text, string fallback, bool usePascalCase)
    {
        var normalized = NormalizeMemberName(text, usePascalCase);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = Regex.Split(text, @"[^\p{L}\p{Nd}]+");
        var builder = new StringBuilder();

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
            {
                builder.Append(word[1..]);
            }
        }

        return builder.ToString();
    }

    private static string SanitizeIdentifier(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
