using AzrngTools.Models.Pdf;
using AzrngTools.Services.Pdf;
using NPOI.XWPF.UserModel;

namespace AzrngTools.Tests.Services.Pdf;

/// <summary>
/// Word 导出还原度用例：字号 / 加粗 / 有线表格 / 图片（DES-PDF-001 升级项）。
/// </summary>
public class AsposePdfProcessingServiceWordExportTests
{
    private readonly AsposePdfProcessingService _service = new();

    [Fact]
    public async Task WordExport_PureTextPdf_ShouldProduceOneParagraphPerLine()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "plain.pdf");
            WordExportFixtureBuilder.CreatePlainTextPdf(sourcePdf);
            var outputDocx = Path.Combine(tempDirectory, "plain.docx");

            var result = await _service.ExportWordAsync(new PdfWordExportRequest(sourcePdf, outputDocx));

            Assert.True(result.IsSuccess, result.Message);
            using var document = new XWPFDocument(File.OpenRead(outputDocx));
            var texts = document.Paragraphs.Select(paragraph => paragraph.Text).ToList();

            Assert.Equal(3, texts.Count);
            Assert.Equal("第一行内容", texts[0]);
            Assert.Equal("第二行内容", texts[1]);
            Assert.Equal("第三行内容", texts[2]);
            Assert.Empty(document.Tables);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task WordExport_ShouldRestoreFontSizeAndBold()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "styled.pdf");
            var outputDocx = Path.Combine(tempDirectory, "styled.docx");
            var jpegPath = Path.Combine(tempDirectory, "pic.jpg");
            WordExportFixtureBuilder.CreateJpeg(jpegPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, jpegPath);

            var result = await _service.ExportWordAsync(new PdfWordExportRequest(sourcePdf, outputDocx));

            Assert.True(result.IsSuccess, result.Message);
            using var document = new XWPFDocument(File.OpenRead(outputDocx));

            var titleRun = document.Paragraphs
                .SelectMany(paragraph => paragraph.Runs.Cast<XWPFRun>())
                .First(run => run.Text.Contains("报告标题"));
            Assert.True(titleRun.IsBold);
            Assert.Equal(22, titleRun.FontSize);

            var bodyRun = document.Paragraphs
                .SelectMany(paragraph => paragraph.Runs.Cast<XWPFRun>())
                .First(run => run.Text.Contains("正文第一行"));
            Assert.False(bodyRun.IsBold);
            Assert.Equal(11, bodyRun.FontSize);

            var boldBodyRun = document.Paragraphs
                .SelectMany(paragraph => paragraph.Runs.Cast<XWPFRun>())
                .First(run => run.Text.Contains("加粗正文行"));
            Assert.True(boldBodyRun.IsBold);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task WordExport_ShouldConvertRuledTableToWordTable()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "table.pdf");
            var outputDocx = Path.Combine(tempDirectory, "table.docx");
            var jpegPath = Path.Combine(tempDirectory, "pic.jpg");
            WordExportFixtureBuilder.CreateJpeg(jpegPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, jpegPath);

            var result = await _service.ExportWordAsync(new PdfWordExportRequest(sourcePdf, outputDocx));

            Assert.True(result.IsSuccess, result.Message);
            using var document = new XWPFDocument(File.OpenRead(outputDocx));

            var table = Assert.Single(document.Tables);
            Assert.Equal(2, table.Rows.Count);
            Assert.Equal(2, table.Rows[0].GetTableICells().Count);
            Assert.Equal(2, table.Rows[1].GetTableICells().Count);
            Assert.Equal("单元格A1", table.GetRow(0).GetCell(0).GetText());
            Assert.Equal("单元格B1", table.GetRow(0).GetCell(1).GetText());
            Assert.Equal("单元格A2", table.GetRow(1).GetCell(0).GetText());
            Assert.Equal("单元格B2", table.GetRow(1).GetCell(1).GetText());
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public async Task WordExport_ShouldEmbedJpegImage()
    {
        var tempDirectory = CreateTempDirectory();

        try
        {
            var sourcePdf = Path.Combine(tempDirectory, "image.pdf");
            var outputDocx = Path.Combine(tempDirectory, "image.docx");
            var jpegPath = Path.Combine(tempDirectory, "pic.jpg");
            WordExportFixtureBuilder.CreateJpeg(jpegPath);
            WordExportFixtureBuilder.CreateStyledPdf(sourcePdf, jpegPath);
            var jpegBytes = await File.ReadAllBytesAsync(jpegPath);

            var result = await _service.ExportWordAsync(new PdfWordExportRequest(sourcePdf, outputDocx));

            Assert.True(result.IsSuccess, result.Message);
            using var document = new XWPFDocument(File.OpenRead(outputDocx));

            var picture = Assert.Single(document.AllPictures);
            // AddPicture 以 jpg 文件名嵌入，NPOI 统一登记为 imageN.jpeg
            Assert.True(
                picture.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                picture.FileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase),
                picture.FileName);
            Assert.Equal(jpegBytes.Length, picture.Data.Length);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "AzrngToolsWordExportTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }
}
