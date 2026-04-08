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
    private readonly IMessageService _messageService;

    public JsonToCsharpPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _rootClassName = "RootObject";
        _nameSpace = "MyApplication.Models";
        _useNullableTypes = true;
        _usePascalCase = true;
        _generateProperties = true;
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
    private void ConvertToCsharp()
    {
        try
        {
            if (JsonInput.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入 JSON 内容");
                return;
            }

            using var jsonDoc = JsonDocument.Parse(JsonInput);
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                _messageService.SendMessage("JSON 根节点必须是对象");
                return;
            }

            var generatedClasses = new HashSet<string>(StringComparer.Ordinal);
            var classDefinitions = new List<string>();
            var rootName = NormalizeClassName(RootClassName, "RootObject");

            GenerateClass(jsonDoc.RootElement, rootName, classDefinitions, generatedClasses);

            var builder = new StringBuilder();
            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine();
            builder.AppendLine($"namespace {NameSpace}");
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
            CsharpOutput = builder.ToString();

            _messageService.SendMessage($"转换成功，生成了 {generatedClasses.Count} 个类");
        }
        catch (JsonException ex)
        {
            _messageService.SendMessage($"JSON 解析失败：{ex.Message}");
        }
        catch (Exception ex)
        {
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
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void FormatJson()
    {
        try
        {
            if (JsonInput.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入 JSON 内容");
                return;
            }

            using var jsonDoc = JsonDocument.Parse(JsonInput);
            JsonInput = JsonSerializer.Serialize(jsonDoc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        }
        catch (JsonException ex)
        {
            _messageService.SendMessage($"JSON 格式化失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"格式化失败：{ex.Message}");
        }
    }

    private void GenerateClass(JsonElement element, string className, List<string> classDefinitions, HashSet<string> generatedClasses)
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
            var memberName = NormalizeMemberName(property.Name);
            var suggestedClassName = NormalizeClassName(memberName, "AnonymousObject");
            var propertyType = ResolveType(property.Value, suggestedClassName, classDefinitions, generatedClasses);

            if (GenerateProperties)
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

    private string ResolveType(JsonElement element, string suggestedClassName, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => ResolveNumberType(element),
            JsonValueKind.True => ResolveNullableValueType("bool"),
            JsonValueKind.False => ResolveNullableValueType("bool"),
            JsonValueKind.Null => "object",
            JsonValueKind.Object => ResolveObjectType(element, suggestedClassName, classDefinitions, generatedClasses),
            JsonValueKind.Array => ResolveArrayType(element, suggestedClassName, classDefinitions, generatedClasses),
            _ => "object"
        };
    }

    private string ResolveObjectType(JsonElement element, string suggestedClassName, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        GenerateClass(element, suggestedClassName, classDefinitions, generatedClasses);
        return suggestedClassName;
    }

    private string ResolveArrayType(JsonElement element, string suggestedClassName, List<string> classDefinitions, HashSet<string> generatedClasses)
    {
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null || item.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            var itemType = item.ValueKind == JsonValueKind.Object
                ? ResolveObjectType(item, $"{suggestedClassName}Item", classDefinitions, generatedClasses)
                : ResolveType(item, $"{suggestedClassName}Item", classDefinitions, generatedClasses);

            return $"List<{itemType}>";
        }

        return "List<object>";
    }

    private string ResolveNumberType(JsonElement element)
    {
        if (element.TryGetInt32(out _))
        {
            return ResolveNullableValueType("int");
        }

        if (element.TryGetInt64(out _))
        {
            return ResolveNullableValueType("long");
        }

        if (element.TryGetDecimal(out _))
        {
            return ResolveNullableValueType("decimal");
        }

        return ResolveNullableValueType("double");
    }

    private string ResolveNullableValueType(string typeName)
    {
        return UseNullableTypes ? $"{typeName}?" : typeName;
    }

    private string NormalizeMemberName(string text)
    {
        var normalized = UsePascalCase ? ToPascalCase(text) : SanitizeIdentifier(text);
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

    private string NormalizeClassName(string text, string fallback)
    {
        var normalized = NormalizeMemberName(text);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private string ToPascalCase(string text)
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

    private string SanitizeIdentifier(string text)
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
