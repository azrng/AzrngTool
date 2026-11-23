using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.XWPF.UserModel;

namespace AzrngTools.Tests.Services.Word;

/// <summary>
/// Word 转 Markdown 测试的 docx 样张构造器：标题 / 加粗斜体删除线 / 表格 / 引用 / 内嵌图片。
/// </summary>
internal static class WordMarkdownFixtureBuilder
{
    /// <summary>1x1 红色像素 PNG，最小合法图片字节。</summary>
    public static byte[] TinyPng { get; } = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>
    /// 生成含 1~3 级标题、加粗/斜体/删除线行内格式、3x3 表格与引用段落的 docx。
    /// </summary>
    public static void CreateStyledDocx(string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        var doc = new XWPFDocument();
        // NPOI 裸文档不写 styles.xml，DocSharp 无 w:name 可查会丢标题——显式补 heading 样式定义
        var styles = doc.CreateStyles();
        for (var level = 1; level <= 3; level++)
        {
            styles.AddStyle(new XWPFStyle(new CT_Style
            {
                styleId = $"Heading{level}",
                type = ST_StyleType.paragraph,
                name = new CT_String { val = $"heading {level}" }
            }));
        }
        // 标题样式用英文 styleId（DocSharp 经 styles.xml w:name="heading N" 解析）
        foreach (var (text, level) in new[] { ("一级标题", 1), ("二级标题", 2), ("三级标题", 3) })
        {
            var paragraph = doc.CreateParagraph();
            paragraph.Style = $"Heading{level}";
            paragraph.CreateRun().SetText(text);
        }

        var inline = doc.CreateParagraph();
        inline.CreateRun().SetText("普通");
        var bold = inline.CreateRun();
        bold.IsBold = true;
        bold.SetText("加粗");
        var italic = inline.CreateRun();
        italic.IsItalic = true;
        italic.SetText("斜体");
        var strike = inline.CreateRun();
        strike.IsStrikeThrough = true;
        strike.SetText("删除线");

        var table = doc.CreateTable(2, 2);
        string[,] cells = { { "列A", "列B" }, { "值1", "值2" } };
        for (var r = 0; r < 2; r++)
        {
            for (var c = 0; c < 2; c++)
            {
                var cellParagraph = table.GetRow(r).GetCell(c).AddParagraph();
                cellParagraph.CreateRun().SetText(cells[r, c]);
            }
        }

        var quote = doc.CreateParagraph();
        quote.Style = "Quote";
        quote.CreateRun().SetText("引用段落");

        doc.Write(fs);
    }

    /// <summary>
    /// 生成含一张内嵌 PNG 图片的 docx。
    /// </summary>
    public static void CreateDocxWithImage(string path)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
        var doc = new XWPFDocument();

        var heading = doc.CreateParagraph();
        heading.Style = "Heading1";
        heading.CreateRun().SetText("带图文档");

        var pictureParagraph = doc.CreateParagraph();
        var run = pictureParagraph.CreateRun();
        run.AddPicture(new MemoryStream(TinyPng), (int)PictureType.PNG, "dot.png", 40, 40);
        run.SetText("（图注）");

        doc.Write(fs);
    }

}
