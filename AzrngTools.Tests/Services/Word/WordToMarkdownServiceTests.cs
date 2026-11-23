using AzrngTools.Models.Word;
using AzrngTools.Services.Word;

namespace AzrngTools.Tests.Services.Word;

/// <summary>
/// Word 转 Markdown 服务用例：标题层级 / 行内格式 / 表格 / 图片导出与忽略 / 锚点清洗 / 失败路径。
/// </summary>
public class WordToMarkdownServiceTests
{
    private readonly WordToMarkdownService _service = new();

    [Fact]
    public async Task Convert_StyledDocx_ShouldMapHeadingsInlineFormatsAndTable()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var docxPath = Path.Combine(tempDirectory, "styled.docx");
            var outputPath = Path.Combine(tempDirectory, "styled.md");
            WordMarkdownFixtureBuilder.CreateStyledDocx(docxPath);

            var result = await _service.ConvertAsync(new WordMarkdownConvertRequest(docxPath, outputPath, ExportImages: true));

            Assert.True(result.IsSuccess, result.Message);
            var data = result.Data!;
            Assert.Equal(3, data.HeadingCount);
            Assert.Contains("# 一级标题", data.Markdown, StringComparison.Ordinal);
            Assert.Contains("## 二级标题", data.Markdown, StringComparison.Ordinal);
            Assert.Contains("### 三级标题", data.Markdown, StringComparison.Ordinal);
            Assert.Contains("**加粗**", data.Markdown, StringComparison.Ordinal);
            Assert.Contains("*斜体*", data.Markdown, StringComparison.Ordinal);
            Assert.Contains("~~删除线~~", data.Markdown, StringComparison.Ordinal);
            // GFM 表格行
            Assert.Contains("| 列A", data.Markdown, StringComparison.Ordinal);
            Assert.Contains("| 值1", data.Markdown, StringComparison.Ordinal);
            Assert.True(File.Exists(outputPath));
            Assert.Contains("# 一级标题", File.ReadAllText(outputPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task Convert_DocxWithImage_ExportMode_ShouldExportAssetAndRelativeLink()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var docxPath = Path.Combine(tempDirectory, "imaged.docx");
            var outputPath = Path.Combine(tempDirectory, "imaged.md");
            WordMarkdownFixtureBuilder.CreateDocxWithImage(docxPath);

            var result = await _service.ConvertAsync(new WordMarkdownConvertRequest(docxPath, outputPath, ExportImages: true));

            Assert.True(result.IsSuccess, result.Message);
            var data = result.Data!;
            Assert.Equal(1, data.ExportedImageCount);
            Assert.Equal("imaged.assets", data.AssetsDirectoryName);
            // md 内为相对引用而非绝对路径
            Assert.Contains("](imaged.assets/", data.Markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("file:///", data.Markdown, StringComparison.Ordinal);
            var assetsDirectory = Path.Combine(tempDirectory, "imaged.assets");
            Assert.True(Directory.Exists(assetsDirectory));
            Assert.Single(Directory.GetFiles(assetsDirectory));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task Convert_DocxWithImage_IgnoreMode_ShouldDropImages()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var docxPath = Path.Combine(tempDirectory, "imaged.docx");
            var outputPath = Path.Combine(tempDirectory, "imaged.md");
            WordMarkdownFixtureBuilder.CreateDocxWithImage(docxPath);

            var result = await _service.ConvertAsync(new WordMarkdownConvertRequest(docxPath, outputPath, ExportImages: false));

            Assert.True(result.IsSuccess, result.Message);
            var data = result.Data!;
            Assert.Equal(0, data.ExportedImageCount);
            Assert.Equal(string.Empty, data.AssetsDirectoryName);
            Assert.DoesNotContain("![", data.Markdown, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "imaged.assets")));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void PostProcessor_TocAnchorNoise_ShouldBeStripped()
    {
        // DocSharp 对 Word 目录书签的输出形态（实测 user 文档）：标题行内混入空锚点
        const string raw = "# <a id=\"_Toc7895\"></a>**一、**<a id=\"_Toc16091298\"></a>**前言**";

        var cleaned = WordMarkdownPostProcessor.Clean(raw);

        Assert.DoesNotContain("<a id=", cleaned, StringComparison.Ordinal);
        Assert.Equal("# **一、****前言**", cleaned);
    }

    [Fact]
    public void PostProcessor_EmptyInput_ShouldReturnEmpty()
    {
        Assert.Equal(string.Empty, WordMarkdownPostProcessor.Clean(string.Empty));
    }

    [Fact]
    public async Task Convert_MissingFile_ShouldFailWithReason()
    {
        var result = await _service.ConvertAsync(new WordMarkdownConvertRequest(
            Path.Combine(Path.GetTempPath(), "not-exist.docx"),
            Path.Combine(Path.GetTempPath(), "not-exist.md"),
            ExportImages: false));

        Assert.False(result.IsSuccess);
        Assert.Contains("不存在", result.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "not-exist.md")));
    }

    [Fact]
    public async Task Convert_LegacyDocExtension_ShouldFailWithHint()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var docPath = Path.Combine(tempDirectory, "legacy.doc");
            await File.WriteAllTextAsync(docPath, "fake");
            var result = await _service.ConvertAsync(new WordMarkdownConvertRequest(docPath, Path.Combine(tempDirectory, "legacy.md"), false));

            Assert.False(result.IsSuccess);
            Assert.Contains(".docx", result.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task Convert_CorruptDocx_ShouldFailWithFriendlyMessage()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var docxPath = Path.Combine(tempDirectory, "corrupt.docx");
            await File.WriteAllBytesAsync(docxPath, [0x00, 0x01, 0x02, 0x03]);
            var result = await _service.ConvertAsync(new WordMarkdownConvertRequest(docxPath, Path.Combine(tempDirectory, "corrupt.md"), false));

            Assert.False(result.IsSuccess);
            Assert.False(File.Exists(Path.Combine(tempDirectory, "corrupt.md")));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task LoadFile_ValidDocx_ShouldReturnSummary()
    {
        var tempDirectory = CreateTempDirectory();
        try
        {
            var docxPath = Path.Combine(tempDirectory, "styled.docx");
            WordMarkdownFixtureBuilder.CreateStyledDocx(docxPath);

            var result = await _service.LoadFileAsync(docxPath);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("styled.docx", result.Data!.FileName);
            Assert.True(result.Data.FileSizeBytes > 0);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AzrngToolsWordMarkdownTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
