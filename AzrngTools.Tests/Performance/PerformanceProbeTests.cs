using System.Diagnostics;
using System.Text;
using AzrngTools.Models.Format;
using AzrngTools.Models.Pdf;
using AzrngTools.Services.Format;
using AzrngTools.Services.Pdf;
using AzrngTools.Tests.TestDoubles;
using AzrngTools.ViewModels.Encode;
using AzrngTools.ViewModels.Format;
using AzrngTools.ViewModels.TextHandle;
using PdfSharp.Pdf;
using Xunit.Abstractions;

namespace AzrngTools.Tests.Performance;

/// <summary>
/// 无头性能探针：以贴近真实规模的输入计时各页面核心命令，定位性能慢点。
/// 计时结果写入测试输出（dotnet test --logger "console;verbosity=detailed" 可见），
/// 只做功能正确性断言，不断言耗时阈值，避免环境差异造成抖动失败。
/// </summary>
public class PerformanceProbeTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceProbeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task JsonFormat_OneMegabyte()
    {
        var json = BuildLargeJson(itemCount: 6_000);
        var viewModel = new JsonPageViewModel(new TestMessageService()) { Original = json };

        await MeasureAsync("JSON格式化 1MB", async () =>
        {
            viewModel.Original = json;
            await viewModel.FormatJsonCommand.ExecuteAsync(null);
        });

        Assert.Contains(Environment.NewLine, viewModel.Original);
    }

    [Fact]
    public async Task JsonCompress_OneMegabyteIndented()
    {
        var indented = System.Text.Json.JsonSerializer.Serialize(
            BuildLargeJsonObject(6_000),
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var viewModel = new JsonPageViewModel(new TestMessageService()) { Original = indented };

        await MeasureAsync("JSON压缩 1MB", async () =>
        {
            viewModel.Original = indented;
            await viewModel.CompressJsonCommand.ExecuteAsync(null);
        });

        Assert.DoesNotContain(Environment.NewLine, viewModel.Original);
    }

    [Fact]
    public async Task JsonToCsharp_TwoThousandUniqueProperties()
    {
        var json = BuildNestedUniquePropertyJson(propertyCount: 2_000);
        var viewModel = new JsonToCsharpPageViewModel(new TestMessageService()) { JsonInput = json };

        await MeasureAsync("JSON转C# 2000独立属性", async () =>
        {
            viewModel.JsonInput = json;
            await viewModel.ConvertToCsharpCommand.ExecuteAsync(null);
        });

        Assert.Contains("class", viewModel.CsharpOutput);
    }

    [Fact]
    public async Task WordCount_FiveHundredKilobytes()
    {
        var text = BuildMixedLanguageText(blockCount: 1_500);
        var viewModel = new WordCountPageViewModel(new TestMessageService()) { InputText = text };

        await MeasureAsync("字数统计 500KB", async () =>
        {
            viewModel.InputText = text;
            await viewModel.CountWordsCommand.ExecuteAsync(null);
        });

        Assert.True(viewModel.TotalCharacters > 0);
    }

    [Fact]
    public async Task HexEncodeDecode_OneMegabyte()
    {
        var original = BuildMixedLanguageText(blockCount: 3_000);
        var viewModel = new HexEncodePageViewModel(new TestMessageService()) { Original = original };

        await MeasureAsync("十六进制编码 1MB", async () =>
        {
            viewModel.Original = original;
            await viewModel.StringToHexCommand.ExecuteAsync(null);
        });

        var encoded = viewModel.HandleText;
        Assert.NotEmpty(encoded);

        var decodeViewModel = new HexEncodePageViewModel(new TestMessageService()) { Original = encoded };
        await MeasureAsync("十六进制解码 2MB+", async () =>
        {
            decodeViewModel.Original = encoded;
            await decodeViewModel.HexToStringCommand.ExecuteAsync(null);
        });

        Assert.Equal(original, decodeViewModel.HandleText);
    }

    [Fact]
    public async Task RegexAnalysis_TwoHundredKilobytesEmailPattern()
    {
        var messageService = new TestMessageService();
        var text = BuildEmailText(lineCount: 8_000);
        var viewModel = new RegexAnalysisViewModel(messageService)
        {
            RegexPattern = @"\b\w+@\w+\.\w+\b",
            TestText = text
        };

        await MeasureAsync("正则分析 200KB", async () =>
        {
            viewModel.TestText = text;
            await viewModel.TestRegexCommand.ExecuteAsync(null);
        });

        // MeasureAsync 预热+3 次测量共执行 4 次，每次成功都会发送一条"匹配完成"消息
        Assert.Equal(4, messageService.Messages.Count);
        Assert.All(messageService.Messages, message => Assert.StartsWith("匹配完成", message.Message));
    }

    [Fact]
    public async Task ChineseConvert_ThreeHundredKilobytes()
    {
        var text = BuildChineseText(blockCount: 2_000);
        var viewModel = new ChineseConvertPageViewModel(new TestMessageService()) { Original = text };

        await MeasureAsync("繁简转换 300KB", () =>
        {
            viewModel.Original = text;
            viewModel.SimplifiedToTraditionalCommand.Execute(null);
            return Task.CompletedTask;
        });

        Assert.NotEqual(text, viewModel.HandleText);
    }

    [Fact]
    public async Task JsonToYaml_FourHundredKilobytes()
    {
        var json = BuildLargeJson(itemCount: 2_500);
        var viewModel = new JsonYamlXmlPageViewModel(new FormatConversionService(), new TestMessageService())
        {
            SourceFormat = FormatLanguage.Json,
            TargetFormat = FormatLanguage.Yaml,
            SourceText = json
        };

        await MeasureAsync("JSON转YAML 400KB", async () =>
        {
            viewModel.SourceText = json;
            await viewModel.ConvertCommand.ExecuteAsync(null);
        });

        Assert.NotEmpty(viewModel.TargetText);
    }

    [Fact]
    public async Task SqlFormat_HundredKilobytes()
    {
        var sql = BuildLargeSql(tableCount: 120);
        var viewModel = new SqlFormatPageViewModel(new TestMessageService()) { OriginText = sql };

        await MeasureAsync("SQL格式化 100KB", async () =>
        {
            viewModel.OriginText = sql;
            await viewModel.SqlFormatCommand.ExecuteAsync(null);
        });

        Assert.Contains("SELECT", viewModel.OriginText.ToUpperInvariant());
    }

    [Fact]
    public async Task JsonSchemaGeneration_TwoHundredKilobytes()
    {
        var json = BuildLargeJson(itemCount: 2_500);
        var viewModel = new JsonSchemaPageViewModel(new TestMessageService()) { OriginText = json };

        await MeasureAsync("JSON Schema生成 200KB", async () =>
        {
            viewModel.OriginText = json;
            await viewModel.SchemaGenHandleCommand.ExecuteAsync(null);
        });

        Assert.NotEmpty(viewModel.SchemaData);
    }

    [Fact]
    public async Task PdfMetadataAndSplit_HundredTwentyPages()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AzrngToolsPerfProbes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "source.pdf");
            CreateSamplePdf(sourcePdf, pageCount: 120);
            var service = new AsposePdfProcessingService();

            var metadata = await service.LoadMetadataAsync(sourcePdf);
            Assert.True(metadata.IsSuccess, metadata.Message);

            await MeasureAsync("PDF拆分 120页→单文件", async () =>
            {
                await service.SplitAsync(new PdfSplitRequest(
                    sourcePdf,
                    Enumerable.Range(1, 120).ToList(),
                    PdfSplitMode.SingleFile,
                    Path.Combine(tempDirectory, "split.pdf")));
            });

            Assert.True(File.Exists(Path.Combine(tempDirectory, "split.pdf")));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }
    }

    private async Task MeasureAsync(string name, Func<Task> action, int iterations = 3)
    {
        await action(); // 预热（JIT、缓存、首次资源加载）

        var timings = new List<long>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            timings.Add(stopwatch.ElapsedMilliseconds);
        }

        _output.WriteLine($"[性能探针] {name}: {string.Join(" / ", timings)} ms（最佳 {timings.Min()} ms）");
    }

    private static object BuildLargeJsonObject(int itemCount) => Enumerable.Range(1, itemCount)
        .Select(index => new Dictionary<string, object>
        {
            ["id"] = index,
            ["name"] = $"工具项_{index}",
            ["enabled"] = index % 2 == 0,
            ["score"] = index * 1.5,
            ["tags"] = new[] { "encode", "format", $"tag_{index % 50}" },
            ["meta"] = new Dictionary<string, object>
            {
                ["created"] = "2026-10-30T08:00:00Z",
                ["level"] = index % 5,
            },
        });

    private static string BuildLargeJson(int itemCount) =>
        System.Text.Json.JsonSerializer.Serialize(BuildLargeJsonObject(itemCount));

    private static string BuildNestedUniquePropertyJson(int propertyCount)
    {
        var builder = new StringBuilder(propertyCount * 48);
        builder.Append('{');
        for (var i = 0; i < propertyCount; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append($"\"UniquePropertyName{i}\":{{\"Value\":{i},\"Note\":\"属性{i}说明\"}}");
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string BuildMixedLanguageText(int blockCount)
    {
        const string block = "快速 brown fox 跳过 lazy dog 12345，" +
                             "The quick brown fox jumps over the lazy dog near the riverbank at dawn. ";
        var builder = new StringBuilder(block.Length * blockCount);
        for (var i = 0; i < blockCount; i++)
        {
            builder.AppendLine(block);
        }

        return builder.ToString();
    }

    private static string BuildChineseText(int blockCount)
    {
        const string block = "性能优化是持续过程，先测量再优化，避免凭感觉改动；" +
                             "简单的直白写法往往更快，复杂的分支需要注释说明原因。";
        var builder = new StringBuilder(block.Length * blockCount);
        for (var i = 0; i < blockCount; i++)
        {
            builder.AppendLine(block);
        }

        return builder.ToString();
    }

    private static string BuildEmailText(int lineCount)
    {
        var builder = new StringBuilder(lineCount * 40);
        for (var i = 0; i < lineCount; i++)
        {
            builder.AppendLine($"user{i}@example.com 联系我们 support+{i}@mail.test 获取帮助");
        }

        return builder.ToString();
    }

    private static string BuildLargeSql(int tableCount)
    {
        var builder = new StringBuilder(tableCount * 400);
        builder.Append("SELECT o.Id, o.CreatedAt, c.CustomerName, c.Phone, p.ProductName, p.Price ");
        builder.Append("FROM [Order] o ");
        builder.Append("INNER JOIN Customer c ON c.Id = o.CustomerId ");
        builder.Append("INNER JOIN Product p ON p.Id = o.ProductId ");
        builder.Append("WHERE 1 = 1 ");
        for (var i = 1; i <= tableCount; i++)
        {
            builder.Append($"OR (o.ProductId IN (SELECT Id FROM Product_{i} WHERE RegionCode = 'R{i:000}' AND Status = 1) AND o.Amount > {i}.50) ");
        }

        builder.Append("ORDER BY o.CreatedAt DESC;");
        return builder.ToString();
    }

    private static void CreateSamplePdf(string path, int pageCount)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            document.AddPage();
        }

        document.Save(path);
    }
}
