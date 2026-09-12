using AzrngTools.Models.Format;
using AzrngTools.Services.Format;
using Xunit;

namespace AzrngTools.Tests.Services.Format;

public class FormatConversionServiceTests
{
    private readonly FormatConversionService _service = new();

    private const string JsonSample =
        """
        {
          "name": "AzrngTools",
          "version": 12,
          "stable": true,
          "tags": ["a", "b"],
          "database": { "host": "localhost", "port": 5432 }
        }
        """;

    [Fact]
    public void JsonYamlRoundTrip_ShouldPreserveStructureAndTypes()
    {
        var yaml = _service.Convert(JsonSample, FormatLanguage.Json, FormatLanguage.Yaml);
        var back = _service.Convert(yaml, FormatLanguage.Yaml, FormatLanguage.Json);

        Assert.Equal(Normalize(JsonSample), Normalize(back));
    }

    [Theory]
    [InlineData(FormatLanguage.Json, FormatLanguage.Yaml)]
    [InlineData(FormatLanguage.Json, FormatLanguage.Xml)]
    [InlineData(FormatLanguage.Yaml, FormatLanguage.Json)]
    [InlineData(FormatLanguage.Yaml, FormatLanguage.Xml)]
    [InlineData(FormatLanguage.Xml, FormatLanguage.Json)]
    [InlineData(FormatLanguage.Xml, FormatLanguage.Yaml)]
    public void Convert_ShouldSupportEveryDirection(FormatLanguage from, FormatLanguage to)
    {
        var source = _service.Convert(JsonSample, FormatLanguage.Json, from);
        var converted = _service.Convert(source, from, to);

        Assert.False(string.IsNullOrWhiteSpace(converted));
    }

    [Fact]
    public void JsonToYaml_ShouldKeepStringQuotingStable()
    {
        const string json = """{ "count": "123", "total": 456, "flag": "true" }""";

        var yaml = _service.Convert(json, FormatLanguage.Json, FormatLanguage.Yaml);
        var back = _service.Convert(yaml, FormatLanguage.Yaml, FormatLanguage.Json);
        var node = System.Text.Json.Nodes.JsonNode.Parse(back)!;

        Assert.Equal("123", node["count"]!.GetValue<string>());
        Assert.Equal(456, node["total"]!.GetValue<int>());
        Assert.Equal("true", node["flag"]!.GetValue<string>());
    }

    [Fact]
    public void XmlToJson_ShouldMapAttributesAndTextByConvention()
    {
        const string xml = """
                           <?xml version="1.0" encoding="utf-8"?>
                           <user id="1" role="admin">Alice<tags><tag>a</tag><tag>b</tag></tags></user>
                           """;

        var json = _service.Convert(xml, FormatLanguage.Xml, FormatLanguage.Json);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;

        Assert.Equal("1", node["@id"]!.GetValue<string>());
        Assert.Equal("admin", node["@role"]!.GetValue<string>());
        Assert.Equal("Alice", node["#text"]!.GetValue<string>());
        Assert.Equal(2, node["tags"]!["tag"]!.AsArray().Count);
    }

    [Fact]
    public void JsonToXml_ShouldRepeatArrayItemsAndSanitizeNames()
    {
        const string json = """{ "user-name": "a", "ports": [1, 2] }""";

        var xml = _service.Convert(json, FormatLanguage.Json, FormatLanguage.Xml);

        Assert.Contains("<user-name>a</user-name>", xml);
        Assert.Contains("<ports>1</ports>", xml);
        Assert.Contains("<ports>2</ports>", xml);
    }

    [Fact]
    public void Validate_ShouldReportLineInfoOnBrokenJson()
    {
        const string broken = """{ "a": 1, }""";

        var result = _service.Validate(broken, FormatLanguage.Json);

        Assert.False(result.IsSuccess);
        Assert.Contains("行", result.Message);
    }

    [Fact]
    public void Validate_ShouldPassWellFormedDocuments()
    {
        Assert.True(_service.Validate("a: 1", FormatLanguage.Yaml).IsSuccess);
        Assert.True(_service.Validate("<a>1</a>", FormatLanguage.Xml).IsSuccess);
        Assert.True(_service.Validate("{\"a\": 1}", FormatLanguage.Json).IsSuccess);
    }

    [Fact]
    public void Format_ShouldIndentJson()
    {
        var formatted = _service.Format("""{"a":1}""", FormatLanguage.Json);

        Assert.Contains("\n", formatted);
        Assert.Contains("\"a\": 1", formatted);
    }

    private static string Normalize(string json)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(json);
        return node?.ToJsonString() ?? string.Empty;
    }
}
