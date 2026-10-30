using System.Text.Json.Nodes;
using AzrngTools.Models.Format;
using AzrngTools.Services.Format;
using Xunit;

namespace AzrngTools.Tests.Services.Format;

/// <summary>
/// ENV 与 JSON/YAML/XML 互转的解析、输出、校验与往返用例。
/// 语法约定见 doc/requirements/format/tools.md 的 ENV 补充规则。
/// </summary>
public class FormatConversionServiceEnvTests
{
    private readonly FormatConversionService _service = new();

    [Fact]
    public void EnvToJson_ShouldParseCommentsExportQuotesAndSpaces()
    {
        const string env = """
                           # 数据库配置
                           export HOST = localhost
                           PORT=5432
                           DEBUG = true   # 行尾注释
                           EMPTY=
                           QUOTED="hello world"
                           SINGLE='keep \n literal'
                           """;

        var node = (JsonObject)JsonNode.Parse(_service.Convert(env, FormatLanguage.Env, FormatLanguage.Json))!;

        Assert.Equal("localhost", node["HOST"]!.GetValue<string>());
        // ENV 值不做类型推断，全部保持字符串
        Assert.Equal("5432", node["PORT"]!.GetValue<string>());
        Assert.Equal("true", node["DEBUG"]!.GetValue<string>());
        Assert.Equal(string.Empty, node["EMPTY"]!.GetValue<string>());
        Assert.Equal("hello world", node["QUOTED"]!.GetValue<string>());
        Assert.Equal("keep \\n literal", node["SINGLE"]!.GetValue<string>());
    }

    [Fact]
    public void EnvToJson_InlineComment_ShouldRequireLeadingWhitespace()
    {
        const string env = """
                           URL=http://a#b
                           OTHER=http://a #说明
                           """;

        var node = (JsonObject)JsonNode.Parse(_service.Convert(env, FormatLanguage.Env, FormatLanguage.Json))!;

        // # 无前置空白属于值的一部分（URL 锚点），有前置空白才是注释
        Assert.Equal("http://a#b", node["URL"]!.GetValue<string>());
        Assert.Equal("http://a", node["OTHER"]!.GetValue<string>());
    }

    [Fact]
    public void EnvToJson_MultilineDoubleQuotedValue_ShouldUnescapeAndJoinLines()
    {
        const string env = """
                           INTRO="第一行
                           第二行\t缩进"
                           AFTER=ok
                           """;

        var node = (JsonObject)JsonNode.Parse(_service.Convert(env, FormatLanguage.Env, FormatLanguage.Json))!;

        Assert.Equal("第一行\n第二行\t缩进", node["INTRO"]!.GetValue<string>());
        // 跨行值之后的行继续正常解析
        Assert.Equal("ok", node["AFTER"]!.GetValue<string>());
    }

    [Fact]
    public void EnvToJson_DuplicateKey_ShouldKeepLastValue()
    {
        const string env = """
                           MODE=a
                           MODE=b
                           """;

        var node = (JsonObject)JsonNode.Parse(_service.Convert(env, FormatLanguage.Env, FormatLanguage.Json))!;

        Assert.Equal("b", node["MODE"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("JUST_A_LINE")]
    [InlineData("=value")]
    [InlineData("KEY=\"未闭合")]
    public void EnvToJson_InvalidInput_ShouldThrowWithLineNumber(string env)
    {
        var exception = Assert.Throws<FormatException>(() => _service.Convert(env, FormatLanguage.Env, FormatLanguage.Json));

        Assert.Contains("行", exception.Message);
    }

    [Fact]
    public void JsonToEnv_ShouldFlattenWithDotAndIndexPaths()
    {
        const string json = """
                           {
                             "database": { "host": "localhost", "port": 5432 },
                             "tags": ["a", "b"],
                             "enabled": true,
                             "ratio": null,
                             "note": "含 # 与 空格"
                           }
                           """;

        var env = _service.Convert(json, FormatLanguage.Json, FormatLanguage.Env);

        Assert.Contains("database.host=localhost", env);
        Assert.Contains("database.port=5432", env);
        Assert.Contains("tags.0=a", env);
        Assert.Contains("tags.1=b", env);
        Assert.Contains("enabled=true", env);
        // null 输出空值
        Assert.Contains("ratio=", env);
        // 含 # 与空格的值加双引号
        Assert.Contains("note=\"含 # 与 空格\"", env);
    }

    [Fact]
    public void JsonEnvRoundTrip_ForFlatObject_ShouldPreserveValues()
    {
        const string json = """{ "A": "plain", "B": "with space", "C": "", "D": 12, "E": true }""";

        var env = _service.Convert(json, FormatLanguage.Json, FormatLanguage.Env);
        var back = _service.Convert(env, FormatLanguage.Env, FormatLanguage.Json);
        var node = JsonNode.Parse(back)!;

        Assert.Equal("plain", node["A"]!.GetValue<string>());
        Assert.Equal("with space", node["B"]!.GetValue<string>());
        Assert.Equal(string.Empty, node["C"]!.GetValue<string>());
        // ENV 侧全为字符串，数字与布尔往返后成为字符串字面量
        Assert.Equal("12", node["D"]!.GetValue<string>());
        Assert.Equal("true", node["E"]!.GetValue<string>());
    }

    [Fact]
    public void EnvFormat_ShouldNormalizeOutput()
    {
        const string env = """
                           # 注释会被去掉
                           export  NAME  =  "Azrng"
                           NUM=007
                           """;

        var normalized = _service.Format(env, FormatLanguage.Env);

        // 每行以换行结尾（含最后一行），注释与 export 前缀被规范化去掉，引号按需输出
        Assert.Equal($"NAME=Azrng{Environment.NewLine}NUM=007{Environment.NewLine}", normalized);
    }

    [Fact]
    public void Validate_Env_ShouldReportFailureWithLine()
    {
        var result = _service.Validate("GOOD=1" + Environment.NewLine + "BAD_LINE", FormatLanguage.Env);

        Assert.False(result.IsSuccess);
        Assert.Contains("行 2", result.Message);
    }

    [Fact]
    public void Validate_Env_ShouldPassForValidContent()
    {
        var result = _service.Validate("KEY=value", FormatLanguage.Env);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void JsonToEnv_NonObjectRoot_ShouldThrow()
    {
        var exception = Assert.Throws<FormatException>(
            () => _service.Convert("[1,2]", FormatLanguage.Json, FormatLanguage.Env));

        Assert.Contains("根节点", exception.Message);
    }

    [Theory]
    [InlineData(FormatLanguage.Json)]
    [InlineData(FormatLanguage.Yaml)]
    [InlineData(FormatLanguage.Xml)]
    [InlineData(FormatLanguage.Env)]
    public void Convert_ShouldSupportEveryDirectionFromEnv(FormatLanguage to)
    {
        const string env = """
                           HOST=localhost
                           PORT=5432
                           """;

        var converted = _service.Convert(env, FormatLanguage.Env, to);
        var back = _service.Convert(converted, to, FormatLanguage.Env);

        // ENV 侧全为字符串值，往返后键值保持
        Assert.Contains("HOST=localhost", back);
        Assert.Contains("PORT=5432", back);
    }

    [Theory]
    [InlineData(FormatLanguage.Json)]
    [InlineData(FormatLanguage.Yaml)]
    [InlineData(FormatLanguage.Xml)]
    public void Convert_ShouldSupportEveryDirectionToEnv(FormatLanguage from)
    {
        var source = _service.Convert("""{ "host": "db", "port": 5432 }""", FormatLanguage.Json, from);

        var env = _service.Convert(source, from, FormatLanguage.Env);

        Assert.Contains("host=db", env);
        Assert.Contains("port=5432", env);
    }
}
