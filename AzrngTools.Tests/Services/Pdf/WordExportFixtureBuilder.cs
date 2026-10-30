using PdfSharp.Drawing;
using PdfSharp.Fonts;
using SkiaSharp;

namespace AzrngTools.Tests.Services.Pdf;

/// <summary>
/// 测试专用字体解析器：PdfSharp 在 .NET 下无内置系统字体解析，
/// 统一映射到系统自带的等线单文件字体（含中文字形）。
/// </summary>
internal sealed class TestFontResolver : IFontResolver
{
    public static readonly TestFontResolver Instance = new();

    private const string RegularFace = "Deng-Regular";
    private const string BoldFace = "Deng-Bold";

    private TestFontResolver()
    {
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        return new FontResolverInfo(isBold ? BoldFace : RegularFace);
    }

    public byte[] GetFont(string faceName)
    {
        return faceName == BoldFace
            ? File.ReadAllBytes(@"C:\Windows\Fonts\Dengb.ttf")
            : File.ReadAllBytes(@"C:\Windows\Fonts\Deng.ttf");
    }
}

/// <summary>
/// Word 导出还原度测试的 PDF 样张构造器：字号 / 加粗 / 有线表格 / JPEG 图片。
/// </summary>
internal static class WordExportFixtureBuilder
{
    static WordExportFixtureBuilder()
    {
        GlobalFontSettings.FontResolver = TestFontResolver.Instance;
    }

    /// <summary>
    /// 生成含大字号加粗标题、普通正文、加粗正文、2×2 有线表格与 JPEG 图片的单页 PDF。
    /// </summary>
    public static void CreateStyledPdf(string path, string jpegPath)
    {
        var document = new PdfSharp.Pdf.PdfDocument();
        var page = document.AddPage();
        using var graphics = XGraphics.FromPdfPage(page);

        graphics.DrawString("报告标题", new XFont("DengXian", 22, XFontStyleEx.Bold), XBrushes.Black, 40, 60);
        graphics.DrawString("正文第一行", new XFont("DengXian", 11), XBrushes.Black, 40, 100);
        graphics.DrawString("加粗正文行", new XFont("DengXian", 11, XFontStyleEx.Bold), XBrushes.Black, 40, 120);

        var pen = XPens.Black;
        graphics.DrawLine(pen, 40, 160, 300, 160);
        graphics.DrawLine(pen, 40, 200, 300, 200);
        graphics.DrawLine(pen, 40, 240, 300, 240);
        graphics.DrawLine(pen, 40, 160, 40, 240);
        graphics.DrawLine(pen, 170, 160, 170, 240);
        graphics.DrawLine(pen, 300, 160, 300, 240);
        graphics.DrawString("单元格A1", new XFont("DengXian", 11), XBrushes.Black, 50, 185);
        graphics.DrawString("单元格B1", new XFont("DengXian", 11), XBrushes.Black, 180, 185);
        graphics.DrawString("单元格A2", new XFont("DengXian", 11), XBrushes.Black, 50, 225);
        graphics.DrawString("单元格B2", new XFont("DengXian", 11), XBrushes.Black, 180, 225);

        using var image = XImage.FromFile(jpegPath);
        graphics.DrawImage(image, 320, 40, 80, 60);

        document.Save(path);
    }

    /// <summary>
    /// 生成仅含三行文本的单页 PDF（无表格线与图片），用于纯文本回归基线。
    /// </summary>
    public static void CreatePlainTextPdf(string path)
    {
        var document = new PdfSharp.Pdf.PdfDocument();
        var page = document.AddPage();
        using var graphics = XGraphics.FromPdfPage(page);

        graphics.DrawString("第一行内容", new XFont("DengXian", 12), XBrushes.Black, 40, 80);
        graphics.DrawString("第二行内容", new XFont("DengXian", 12), XBrushes.Black, 40, 110);
        graphics.DrawString("第三行内容", new XFont("DengXian", 12), XBrushes.Black, 40, 140);

        document.Save(path);
    }

    public static void CreateJpeg(string path)
    {
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
