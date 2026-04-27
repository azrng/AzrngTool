using AzrngTools.Utils;
using Newtonsoft.Json;

namespace AzrngTools.Tests.Utils;

public class JsonHelperTests
{
    [Fact]
    public void JsonFormatter_ShouldFormatJsonSchemaContainingRefAndSchema()
    {
        const string json = """
                            {"$ref":"#/definitions/Welcome10","$schema":"http://json-schema.org/draft-06/schema#","definitions":{"Welcome10":{"type":"object","title":"Welcome10","properties":{"DefaultOrgCode":{"type":"string"},"IsDisabledDrView":{"type":"boolean"}},"additionalProperties":false}}}
                            """;

        var result = JsonHelper.JsonFormatter(json);

        Assert.Contains(Environment.NewLine + "  \"$ref\": \"#/definitions/Welcome10\",", result);
        Assert.Contains(Environment.NewLine + "  \"$schema\": \"http://json-schema.org/draft-06/schema#\",", result);
        Assert.Contains("\"additionalProperties\": false", result);
    }

    [Fact]
    public void JsonCompress_ShouldReturnSingleLineJsonForValidInput()
    {
        const string json = """
                            {
                              "name": "Azrng",
                              "items": [
                                1,
                                2
                              ]
                            }
                            """;

        var result = JsonHelper.JsonCompress(json);

        Assert.Equal("{\"name\":\"Azrng\",\"items\":[1,2]}", result);
    }

    [Fact]
    public void JsonCompress_ShouldThrowForInvalidJson()
    {
        const string json = """
                            {
                              "name":
                            }
                            """;

        Assert.Throws<JsonReaderException>(() => JsonHelper.JsonCompress(json));
    }

    [Fact]
    public void EscapeJsonText_ShouldEscapeQuotesAndNewLines()
    {
        const string text = "{\n  \"name\": \"Azrng\"\n}";

        var result = JsonHelper.EscapeJsonText(text);

        Assert.Equal("{\\n  \\\"name\\\": \\\"Azrng\\\"\\n}", result);
    }

    [Fact]
    public void UnescapeJsonText_ShouldHandleRawEscapedJson()
    {
        const string text = """{\"name\":\"Azrng\",\"items\":[1,2],\"enabled\":true}""";

        var result = JsonHelper.UnescapeJsonText(text);

        Assert.Contains(Environment.NewLine + "  \"name\": \"Azrng\",", result);
        Assert.Contains("\"enabled\": true", result);
    }

    [Fact]
    public void UnescapeJsonText_ShouldHandleWrappedEscapedJsonString()
    {
        const string text = "\"{\\\"name\\\":\\\"Azrng\\\",\\\"items\\\":[1,2],\\\"enabled\\\":true}\"";

        var result = JsonHelper.UnescapeJsonText(text);

        Assert.Contains(Environment.NewLine + "  \"items\": [", result);
        Assert.Contains("\"enabled\": true", result);
    }

    [Fact]
    public void UnescapeJsonText_ShouldThrowFormatExceptionForInvalidEscapedText()
    {
        const string text = "{\\\"name\\\":\\q}";

        Assert.Throws<FormatException>(() => JsonHelper.UnescapeJsonText(text));
    }
}
