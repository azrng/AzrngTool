using AzrngTools.Models.Pdf;
using AzrngTools.Services.Pdf;
using NPOI.XWPF.UserModel;

namespace AzrngTools.Tests.Services.Pdf.MarkdownExport;

/// <summary>
/// Markdown 导出端到端用例：标题/加粗/表格/图片（JPEG 与 PNG）/图片型拒绝（DES-PDF-002）。
/// </summary>
public class PdfMarkdownExportServiceTests
{
    private readonly AsposePdfProcessingService _service = new();

    [Fact]
    public async Task MarkdownExport_ShouldRestoreHeadingBoldTableAndImage()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "styled.pdf");
            var jpegPath = Path.Combine(tempDirectory, "pic.jpg");
            WordExportFixtureBuilder.CreateJpeg(jpegPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, jpegPath);
            var outputMd = Path.Combine(tempDirectory, "styled.md");

            var result = await _service.ExportMarkdownAsync(new PdfMarkdownExportRequest(sourcePdf, outputMd, ExportImages: true));

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(1, result.Data!.ExportedImageCount);
            Assert.Equal(1, result.Data.HeadingCount);
            Assert.Contains("Markdown 导出完成", result.Message);

            var markdown = await File.ReadAllTextAsync(outputMd);
            // 标题 22pt（正文 11pt 的 2 倍）→ h1；标题词加粗 → run 包裹
            var headingLine = markdown
                .Split('\n')
                .First(line => line.Contains("报告标题"));
            Assert.StartsWith("# ", headingLine.TrimStart());
            Assert.Contains("**加粗正文行**", markdown);
            Assert.Contains("| 单元格A1 | 单元格B1 |", markdown);
            Assert.Contains("| --- | --- |", markdown);
            Assert.Contains("![图1](styled.assets/img-1.jpg)", markdown);
            Assert.True(File.Exists(Path.Combine(tempDirectory, "styled.assets", "img-1.jpg")));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task MarkdownExport_ShouldDecodePngImage()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "png.pdf");
            var pngPath = Path.Combine(tempDirectory, "pic.png");
            WordExportFixtureBuilder.CreatePng(pngPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, pngPath);
            var outputMd = Path.Combine(tempDirectory, "png.md");

            var result = await _service.ExportMarkdownAsync(new PdfMarkdownExportRequest(sourcePdf, outputMd, ExportImages: true));

            Assert.True(result.IsSuccess, result.Message);
            var markdown = await File.ReadAllTextAsync(outputMd);
            Assert.Contains("![图1](png.assets/img-1.png)", markdown);

            var pngBytes = await File.ReadAllBytesAsync(Path.Combine(tempDirectory, "png.assets", "img-1.png"));
            Assert.Equal((byte)0x89, pngBytes[0]);
            Assert.Equal((byte)0x50, pngBytes[1]);
            Assert.Equal((byte)0x4E, pngBytes[2]);
            Assert.Equal((byte)0x47, pngBytes[3]);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task MarkdownExport_IgnoreImages_ShouldNotCreateAssets()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "styled.pdf");
            var jpegPath = Path.Combine(tempDirectory, "pic.jpg");
            WordExportFixtureBuilder.CreateJpeg(jpegPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, jpegPath);
            var outputMd = Path.Combine(tempDirectory, "styled.md");

            var result = await _service.ExportMarkdownAsync(new PdfMarkdownExportRequest(sourcePdf, outputMd, ExportImages: false));

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(0, result.Data!.ExportedImageCount);
            Assert.False(Directory.Exists(Path.Combine(tempDirectory, "styled.assets")));

            var markdown = await File.ReadAllTextAsync(outputMd);
            Assert.DoesNotContain("![", markdown);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task MarkdownExport_PlainText_ShouldNotProduceHeadings()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "plain.pdf");
            WordExportFixtureBuilder.CreatePlainTextPdf(sourcePdf);
            var outputMd = Path.Combine(tempDirectory, "plain.md");

            var result = await _service.ExportMarkdownAsync(new PdfMarkdownExportRequest(sourcePdf, outputMd, ExportImages: true));

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(0, result.Data!.HeadingCount);

            var markdown = await File.ReadAllTextAsync(outputMd);
            Assert.Contains("第一行内容", markdown);
            Assert.DoesNotContain("#", markdown);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task MarkdownExport_ImageOnlyPdf_ShouldFailWithoutOutputFile()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "scan.pdf");
            var pngPath = Path.Combine(tempDirectory, "pic.png");
            WordExportFixtureBuilder.CreatePng(pngPath);
            WordExportFixtureBuilder.CreateImageOnlyPdf(sourcePdf, pngPath);
            var outputMd = Path.Combine(tempDirectory, "scan.md");

            var result = await _service.ExportMarkdownAsync(new PdfMarkdownExportRequest(sourcePdf, outputMd, ExportImages: true));

            Assert.False(result.IsSuccess);
            Assert.Contains("无可提取文本", result.Message);
            Assert.False(File.Exists(outputMd));
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task MarkdownExport_NonMarkdownOutputPath_ShouldFailValidation()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "plain.pdf");
            WordExportFixtureBuilder.CreatePlainTextPdf(sourcePdf);
            var outputTxt = Path.Combine(tempDirectory, "plain.txt");

            var result = await _service.ExportMarkdownAsync(new PdfMarkdownExportRequest(sourcePdf, outputTxt, ExportImages: true));

            Assert.False(result.IsSuccess);
            Assert.Contains(".md", result.Message);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task WordExport_ShouldEmbedPngImageAfterFormatExtension()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "png.pdf");
            var pngPath = Path.Combine(tempDirectory, "pic.png");
            WordExportFixtureBuilder.CreatePng(pngPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, pngPath);
            var outputDocx = Path.Combine(tempDirectory, "png.docx");

            var result = await _service.ExportWordAsync(new PdfWordExportRequest(sourcePdf, outputDocx));

            Assert.True(result.IsSuccess, result.Message);
            using var document = new XWPFDocument(File.OpenRead(outputDocx));
            var picture = Assert.Single(document.AllPictures);
            Assert.True(picture.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase), picture.FileName);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AzrngToolsMarkdownExportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
