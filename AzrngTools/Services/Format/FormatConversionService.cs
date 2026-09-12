using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using AzrngTools.Models.Format;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace AzrngTools.Services.Format;

/// <summary>
/// JSON / YAML / XML 互转引擎：三种格式统一先解析为 JsonNode 中间模型再输出目标格式。
/// XML 与对象模型互转采用常见约定：属性映射为 @名称 键，元素文本映射为 #text 键。
/// </summary>
public sealed class FormatConversionService : IFormatConversionService, ISingletonDependency
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string Convert(string source, FormatLanguage from, FormatLanguage to)
    {
        if (from == to)
        {
            return Format(source, from);
        }

        var root = ParseToJsonNode(source, from);
        return to switch
        {
            FormatLanguage.Json => root?.ToJsonString(IndentedJsonOptions) ?? "null",
            FormatLanguage.Yaml => YamlFromJsonNode(root),
            FormatLanguage.Xml => XmlFromJsonNode(root),
            _ => throw new ArgumentOutOfRangeException(nameof(to), to, null)
        };
    }

    public string Format(string source, FormatLanguage language)
    {
        return language switch
        {
            FormatLanguage.Json => FormatJson(source),
            FormatLanguage.Yaml => YamlFromJsonNode(ParseToJsonNode(source, FormatLanguage.Yaml)),
            FormatLanguage.Xml => FormatXml(source),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    public ConversionCheckResult Validate(string source, FormatLanguage language)
    {
        try
        {
            ParseToJsonNode(source, language);
            return ConversionCheckResult.Success($"{language} 语法校验通过。");
        }
        catch (FormatException ex)
        {
            return ConversionCheckResult.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ConversionCheckResult.Failure(ex.Message);
        }
        catch (JsonException ex)
        {
            return ConversionCheckResult.Failure($"JSON 解析失败（行 {ex.LineNumber + 1}，列 {ex.BytePositionInLine}）：{ex.Message}");
        }
        catch (XmlException ex)
        {
            return ConversionCheckResult.Failure($"XML 解析失败（行 {ex.LineNumber}，列 {ex.LinePosition}）：{ex.Message}");
        }
        catch (YamlException ex)
        {
            return ConversionCheckResult.Failure($"YAML 解析失败（行 {ex.Start.Line}，列 {ex.Start.Column}）：{ex.Message}");
        }
    }

    /// <summary>
    /// 统一解析入口：各格式先解析为 JsonNode 中间模型，解析失败抛出带行号信息的异常。
    /// </summary>
    private static JsonNode? ParseToJsonNode(string source, FormatLanguage language)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new FormatException("输入内容为空，请先粘贴要处理的文本。");
        }

        return language switch
        {
            FormatLanguage.Json => ParseJson(source),
            FormatLanguage.Yaml => ParseYaml(source),
            FormatLanguage.Xml => XmlToJsonNode(source),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    private static JsonNode? ParseJson(string source)
    {
        try
        {
            return JsonNode.Parse(source);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"JSON 解析失败（行 {ex.LineNumber + 1}，列 {ex.BytePositionInLine}）：{ex.Message}");
        }
    }

    private static string FormatJson(string source)
    {
        try
        {
            return JsonNode.Parse(source)!.ToJsonString(IndentedJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"JSON 解析失败（行 {ex.LineNumber + 1}，列 {ex.BytePositionInLine}）：{ex.Message}");
        }
    }

    private static JsonNode? ParseYaml(string source)
    {
        var stream = new YamlStream();
        try
        {
            using var reader = new StringReader(source);
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new FormatException($"YAML 解析失败（行 {ex.Start.Line}，列 {ex.Start.Column}）：{ex.Message}");
        }

        if (stream.Documents.Count == 0)
        {
            throw new FormatException("YAML 内容为空。");
        }

        return YamlNodeToJsonNode(stream.Documents[0].RootNode);
    }

    private static string FormatXml(string source)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(source);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"XML 解析失败（行 {ex.LineNumber}，列 {ex.LinePosition}）：{ex.Message}");
        }

        using var writer = new StringWriter();
        document.Save(writer, SaveOptions.None);
        return writer.ToString();
    }

    #region YAML 与 JsonNode 互转

    private static JsonNode? YamlNodeToJsonNode(YamlNode node)
    {
        return node switch
        {
            YamlMappingNode mapping => ObjectFromYamlMapping(mapping),
            YamlSequenceNode sequence => ArrayFromYamlSequence(sequence),
            YamlScalarNode scalar => ScalarFromYaml(scalar),
            _ => JsonValue.Create(node.ToString())
        };
    }

    private static JsonObject ObjectFromYamlMapping(YamlMappingNode mapping)
    {
        var result = new JsonObject();
        foreach (var entry in mapping.Children)
        {
            result[entry.Key.ToString()] = YamlNodeToJsonNode(entry.Value);
        }

        return result;
    }

    private static JsonArray ArrayFromYamlSequence(YamlSequenceNode sequence)
    {
        var result = new JsonArray();
        foreach (var item in sequence.Children)
        {
            result.Add(YamlNodeToJsonNode(item));
        }

        return result;
    }

    /// <summary>
    /// YAML 纯文本标量做类型推断；带引号的标量始终按字符串处理，避免 "123" 被误转成数字。
    /// </summary>
    private static JsonNode? ScalarFromYaml(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted)
        {
            return JsonValue.Create(value);
        }

        if (value.Length == 0 || value is "~" or "null" or "Null" or "NULL")
        {
            return null;
        }

        if (value is "true" or "True" or "TRUE")
        {
            return JsonValue.Create(true);
        }

        if (value is "false" or "False" or "FALSE")
        {
            return JsonValue.Create(false);
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return JsonValue.Create(intValue);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return JsonValue.Create(doubleValue);
        }

        return JsonValue.Create(value);
    }

    private static string YamlFromJsonNode(JsonNode? root)
    {
        var yamlNode = JsonNodeToYaml(root);
        var stream = new YamlStream(new YamlDocument(yamlNode));
        using var writer = new StringWriter();
        stream.Save(writer);
        return writer.ToString();
    }

    private static YamlNode JsonNodeToYaml(JsonNode? node)
    {
        return node switch
        {
            JsonObject jsonObject => MappingFromJsonObject(jsonObject),
            JsonArray jsonArray => SequenceFromJsonArray(jsonArray),
            JsonValue jsonValue => ScalarFromJsonValue(jsonValue),
            _ => new YamlScalarNode(string.Empty)
        };
    }

    private static YamlMappingNode MappingFromJsonObject(JsonObject jsonObject)
    {
        var mapping = new YamlMappingNode();
        foreach (var entry in jsonObject)
        {
            mapping.Children.Add(new YamlScalarNode(entry.Key), JsonNodeToYaml(entry.Value));
        }

        return mapping;
    }

    private static YamlSequenceNode SequenceFromJsonArray(JsonArray jsonArray)
    {
        var sequence = new YamlSequenceNode();
        foreach (var item in jsonArray)
        {
            sequence.Children.Add(JsonNodeToYaml(item));
        }

        return sequence;
    }

    /// <summary>
    /// JSON 字符串若形如数字、布尔或 null，输出 YAML 时强制加引号，保证往返转换类型不漂移。
    /// </summary>
    private static YamlScalarNode ScalarFromJsonValue(JsonValue value)
    {
        if (value.TryGetValue<bool>(out var boolValue))
        {
            return new YamlScalarNode(boolValue ? "true" : "false");
        }

        if (value.TryGetValue<long>(out var longValue))
        {
            return new YamlScalarNode(longValue.ToString(CultureInfo.InvariantCulture));
        }

        if (value.TryGetValue<double>(out var doubleValue))
        {
            return new YamlScalarNode(doubleValue.ToString("R", CultureInfo.InvariantCulture));
        }

        var text = value.TryGetValue<string>(out var stringValue) ? stringValue : value.ToJsonString();
        return NeedsQuoting(text)
            ? new YamlScalarNode(text) { Style = ScalarStyle.DoubleQuoted }
            : new YamlScalarNode(text);
    }

    private static bool NeedsQuoting(string text)
    {
        return text.Length == 0
               || text is "~" or "null" or "true" or "false"
               || long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
               || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    #endregion

    #region XML 与 JsonNode 互转

    /// <summary>
    /// XML 约定：属性输出为 @名称，元素文本输出为 #text；仅有文本的元素直接退化为标量。
    /// </summary>
    private static JsonNode? XmlToJsonNode(string source)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(source);
        }
        catch (XmlException ex)
        {
            throw new FormatException($"XML 解析失败（行 {ex.LineNumber}，列 {ex.LinePosition}）：{ex.Message}");
        }

        return document.Root is null ? null : ElementToJsonNode(document.Root);
    }

    private static JsonNode? ElementToJsonNode(XElement element)
    {
        var attributes = element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration).ToList();
        var children = element.Elements().ToList();
        // 仅取元素的直接文本节点，避免混合内容把后代文本也拼接进来
        var text = string.Concat(element.Nodes().OfType<XText>().Select(node => node.Value)).Trim();

        if (attributes.Count == 0 && children.Count == 0)
        {
            return text.Length == 0 ? null : JsonValue.Create(text);
        }

        var result = new JsonObject();
        foreach (var attribute in attributes)
        {
            result[$"@{attribute.Name.LocalName}"] = JsonValue.Create(attribute.Value);
        }

        foreach (var group in children.GroupBy(child => child.Name.LocalName))
        {
            var values = group.Select(ElementToJsonNode).ToArray();
            result[group.Key] = values.Length == 1 ? values[0] : new JsonArray(values);
        }

        if (text.Length > 0)
        {
            result["#text"] = JsonValue.Create(text);
        }

        return result;
    }

    private static string XmlFromJsonNode(JsonNode? root)
    {
        var rootElement = new XElement("root");
        AppendJsonNodeToElement(rootElement, root);
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), rootElement);
        using var writer = new StringWriter();
        document.Save(writer, SaveOptions.None);
        return writer.ToString();
    }

    private static void AppendJsonNodeToElement(XElement element, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                AppendJsonObjectToElement(element, jsonObject);
                break;
            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    var itemElement = new XElement("item");
                    AppendJsonNodeToElement(itemElement, item);
                    element.Add(itemElement);
                }

                break;
            case JsonValue jsonValue:
                element.Value = JsonScalarToText(jsonValue);
                break;
            default:
                break;
        }
    }

    private static void AppendJsonObjectToElement(XElement element, JsonObject jsonObject)
    {
        foreach (var entry in jsonObject)
        {
            if (entry.Key.StartsWith('@'))
            {
                var attributeName = SanitizeXmlName(entry.Key[1..]);
                element.SetAttributeValue(attributeName, JsonScalarToText(entry.Value));
                continue;
            }

            if (entry.Key == "#text")
            {
                element.Value = JsonScalarToText(entry.Value);
                continue;
            }

            var childName = SanitizeXmlName(entry.Key);
            switch (entry.Value)
            {
                case JsonArray jsonArray:
                    foreach (var item in jsonArray)
                    {
                        var itemElement = new XElement(childName);
                        AppendJsonNodeToElement(itemElement, item);
                        element.Add(itemElement);
                    }

                    break;
                default:
                    var childElement = new XElement(childName);
                    AppendJsonNodeToElement(childElement, entry.Value);
                    element.Add(childElement);
                    break;
            }
        }
    }

    private static string JsonScalarToText(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                return text;
            }

            return node.ToJsonString();
        }

        return node is null ? string.Empty : node.ToJsonString();
    }

    /// <summary>
    /// XML 元素名只允许字母、数字与少量符号，非法字符统一替换为下划线；空键退化为 item。
    /// </summary>
    private static string SanitizeXmlName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "item";
        }

        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (char.IsLetterOrDigit(character) || character is '_' or '-' or '.')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        var sanitized = builder.ToString();
        if (sanitized.Length > 0 && !char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = "_" + sanitized;
        }

        return sanitized.Length == 0 ? "item" : sanitized;
    }

    #endregion
}
