using AzrngTools.Services.Pdf;
using AzrngTools.Services.Pdf.MarkdownExport;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.PdfFonts;

namespace AzrngTools.Tests.Services.Pdf.MarkdownExport;

/// <summary>
/// PdfMarkdownComposer 纯映射逻辑用例：三级标题判定、run 分组、段落合并、GFM 表格（DES-PDF-002）。
/// </summary>
public class PdfMarkdownComposerTests
{
    [Theory]
    [InlineData(0, 92, 1)]
    [InlineData(1, 88, 2)]
    [InlineData(5, 82, 6)]
    public void ResolveHeadingLevel_BookmarkTopHit_ShouldUseBookmarkLevel(int bookmarkLevel, double bookmarkTop, int expectedLevel)
    {
        // 行 Top=100（底边距页顶），词高 10 → 行顶边=90；书签 Top 按顶边口径匹配
        var page = PageInput(bookmarks: [new PdfHeadingBookmark(bookmarkLevel, bookmarkTop)]);
        var line = Line(top: 100, W("标题文字", size: 10));
        var anchors = PdfMarkdownComposer.ResolveBookmarkAnchors([line], page.Bookmarks);

        var level = PdfMarkdownComposer.ResolveHeadingLevel(line, page, bodyFontSize: 10, anchors);

        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void ResolveBookmarkAnchors_SameBaselineBlocks_ShouldAnchorOnlyNearestLinePerBookmark()
    {
        // 密集排版页（版本历史/索引）同基线被切成多块：3 行同 Top（词高 10 → 顶边 135.6）、1 个书签
        var lineA = Line(top: 145.6, W("更新 补丁。", size: 9));
        var lineB = Line(top: 145.6, W("General: unicode-math .", size: 9));
        var lineC = Line(top: 145.6, W(". . . . . .", size: 9));
        var lineD = Line(top: 300, W("普通正文", size: 9));

        var anchors = PdfMarkdownComposer.ResolveBookmarkAnchors(
            [lineA, lineB, lineC, lineD],
            [new PdfHeadingBookmark(1, 136.0)]);

        // 同基线 3 行共享 1 个书签：只锚定距离最近的一行，远处的正文行不受影响
        Assert.Single(anchors);
        Assert.Equal(2, anchors.Values.Single());
        Assert.DoesNotContain(anchors.Keys, key => ReferenceEquals(key, lineD));

        // 书签 Top 与行顶边精确相等时同样只锚定一行
        var anchors2 = PdfMarkdownComposer.ResolveBookmarkAnchors(
            [lineA, lineB, lineC, lineD],
            [new PdfHeadingBookmark(1, 135.6)]);

        Assert.Single(anchors2);
        Assert.Contains(anchors2.Keys, key => ReferenceEquals(key, lineA) || ReferenceEquals(key, lineB) || ReferenceEquals(key, lineC));
        Assert.DoesNotContain(anchors2.Keys, key => ReferenceEquals(key, lineD));
    }

    [Fact]
    public void ResolveBookmarkAnchors_MultipleBookmarksNearSameLine_ShouldNotOccupySameLineTwice()
    {
        var line = Line(top: 100, W("标题", size: 12));
        var farLine = Line(top: 400, W("正文", size: 12));

        // 行顶边 = 100 − 10 = 90；两个书签都落在容差内
        var anchors = PdfMarkdownComposer.ResolveBookmarkAnchors(
            [line, farLine],
            [new PdfHeadingBookmark(0, 90.5), new PdfHeadingBookmark(1, 91.5)]);

        // 最近配对占用后，第二个书签不再挤占同一行
        Assert.Single(anchors);
        Assert.Equal(1, anchors[line]);
    }

    [Fact]
    public void ResolveHeadingLevel_TaggedHeading_ShouldMatchByFirstLetterReference()
    {
        var letter = CreateLetter("标");
        var page = PageInput(tagged: [new PdfTaggedHeading(3, [letter])]);
        var line = Line(top: 100, W("标题", size: 10, firstLetter: letter));

        var level = PdfMarkdownComposer.ResolveHeadingLevel(line, page, bodyFontSize: 10);

        Assert.Equal(3, level);
    }

    [Fact]
    public void ResolveHeadingLevel_TaggedHeading_OtherLetter_ShouldNotMatch()
    {
        var headingLetter = CreateLetter("标");
        var bodyLetter = CreateLetter("正");
        var page = PageInput(tagged: [new PdfTaggedHeading(3, [headingLetter])]);
        var line = Line(top: 100, W("正文", size: 10, firstLetter: bodyLetter));

        // 正文 10pt = 正文字号，字号兜底也不命中
        var level = PdfMarkdownComposer.ResolveHeadingLevel(line, page, bodyFontSize: 10);

        Assert.Null(level);
    }

    [Theory]
    [InlineData(18, 1)]
    [InlineData(15, 2)]
    [InlineData(13.6, 3)]
    public void ResolveHeadingLevel_FontSizeFallback_ShouldUseRatioBuckets(double fontSize, int expectedLevel)
    {
        var line = Line(top: 100, W("标题文本", size: fontSize));

        var level = PdfMarkdownComposer.ResolveHeadingLevel(line, PageInput(), bodyFontSize: 10);

        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void ResolveHeadingLevel_FontSizeFallback_ShouldSuppressJunkLines()
    {
        // 单字符行（行号/上标误报源）
        var singleChar = PdfMarkdownComposer.ResolveHeadingLevel(Line(top: 100, W("3", size: 20)), PageInput(), 10);
        Assert.Null(singleChar);

        // 纯数字/符号行
        var pureDigits = PdfMarkdownComposer.ResolveHeadingLevel(Line(top: 100, W("12345", size: 20)), PageInput(), 10);
        Assert.Null(pureDigits);

        // 超长行（正文误判源）
        var longText = new string('正', 61);
        var tooLong = PdfMarkdownComposer.ResolveHeadingLevel(Line(top: 100, W(longText, size: 20)), PageInput(), 10);
        Assert.Null(tooLong);

        // 行内字号不均匀（标题混上标的误判源）
        var uneven = PdfMarkdownComposer.ResolveHeadingLevel(
            Line(top: 100, W("标题", size: 20), W("上标", size: 10)), PageInput(), 10);
        Assert.Null(uneven);

        // 词数超限
        var tooManyWords = PdfMarkdownComposer.ResolveHeadingLevel(
            Line(top: 100, Enumerable.Range(0, 13).Select(i => W($"词{i}", size: 20)).ToArray()), PageInput(), 10);
        Assert.Null(tooManyWords);
    }

    [Fact]
    public void BuildRuns_ShouldGroupBoldItalicAndJoinCjkWithoutSpace()
    {
        var runs = PdfMarkdownComposer.BuildRuns(
            [W("加粗", size: 11, bold: true), W("正文", size: 11), W("斜体", size: 11, italic: true)],
            []);

        var text = string.Concat(runs);
        // 中文衔接按设计不加空格，run 边界处同样保持
        Assert.Equal("**加粗**正文*斜体*", text);
    }

    [Fact]
    public void BuildRuns_ShouldEscapeInlineMarkdownCharacters()
    {
        var runs = PdfMarkdownComposer.BuildRuns([W("a*b[c]d`e", size: 11)], []);

        Assert.Equal("a\\*b\\[c\\]d\\`e", string.Concat(runs));
    }

    [Fact]
    public void JoinParagraph_ShouldOmitSpaceBetweenCjkAndKeepBetweenLatin()
    {
        Assert.Equal("中文续行", PdfMarkdownComposer.JoinParagraph("中文", "续行"));
        Assert.Equal("Hello World", PdfMarkdownComposer.JoinParagraph("Hello", "World"));
        Assert.Equal("完成。", PdfMarkdownComposer.JoinParagraph("完成", "。"));
    }

    [Fact]
    public void RenderTable_ShouldProduceGfmWithHeaderSeparatorAndEscapedPipe()
    {
        var grid = CreateGrid(
        [
            ["表头A", "表头B"],
            ["含|竖线", "普通"]
        ]);

        var markdown = PdfMarkdownComposer.RenderTable(grid);

        var expected = "| 表头A | 表头B |\n| --- | --- |\n| 含\\|竖线 | 普通 |";
        Assert.Equal(expected, markdown);
    }

    [Fact]
    public void DetectBodyFontSize_ShouldPickMostCommonBucket()
    {
        var words = new List<PdfLayoutWord>();
        for (var i = 0; i < 5; i++)
        {
            words.Add(W("正文内容较多字符", size: 11));
        }
        words.Add(W("标题", size: 20));
        var page = PageInput(blocks: [new PdfTextLineBlock(words, 100)]);

        Assert.Equal(11, PdfMarkdownComposer.DetectBodyFontSize([page]));
    }

    [Fact]
    public void Compose_ShouldRenderHeadingCountPageSeparatorsParagraphMergeAndImageRef()
    {
        var imageBlock = new PdfImageBlock([1, 2, 3], PdfImageFormat.Png, 100, 80, 500);
        var page1 = PageInput(
            blocks:
            [
                new PdfTextLineBlock([W("章节标题", size: 20)], 100),
                new PdfTextLineBlock([W("第一段", size: 10)], 120),
                new PdfTextLineBlock([W("同段续行", size: 10)], 125),
                imageBlock
            ],
            bookmarks: [new PdfHeadingBookmark(0, 90)],
            images: [new PdfMarkdownImageRef(imageBlock, "文档.assets/img-1.png")]);

        var page2 = PageInput(blocks:
        [
            new PdfTextLineBlock([W("第二页正文", size: 10)], 100),
        ]);

        var result = PdfMarkdownComposer.Compose([page1, page2]);

        Assert.Equal(1, result.HeadingCount);
        Assert.Contains("# 章节标题", result.Markdown);
        // 行距 5pt ≤ 行高 ×0.9，两行合并为一段；中文衔接不加空格
        Assert.Contains("第一段同段续行", result.Markdown);
        Assert.Contains("![图1](文档.assets/img-1.png)", result.Markdown);
        Assert.Contains("<!-- 第 2 页 -->", result.Markdown);
        Assert.Contains("第二页正文", result.Markdown);
    }

    private static PdfLayoutWord W(string text, double size = 10, bool bold = false, bool italic = false, Letter? firstLetter = null)
    {
        return new PdfLayoutWord(text, bold, italic, size, new PdfRectangle(0, 0, text.Length * 10, 10), firstLetter);
    }

    private static PdfTextLineBlock Line(double top, params PdfLayoutWord[] words)
    {
        return new PdfTextLineBlock(words, top);
    }

    private static PdfPageComposeInput PageInput(
        IReadOnlyList<PdfLayoutBlock>? blocks = null,
        IReadOnlyList<Hyperlink>? links = null,
        IReadOnlyList<PdfHeadingBookmark>? bookmarks = null,
        IReadOnlyList<PdfTaggedHeading>? tagged = null,
        IReadOnlyList<PdfMarkdownImageRef>? images = null)
    {
        return new PdfPageComposeInput(blocks ?? [], links ?? [], bookmarks ?? [], tagged ?? [], images ?? []);
    }

    private static PdfTableGrid CreateGrid(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var cellTexts = rows
            .Select(row => row.Select(cell => (IReadOnlyList<string>)new List<string> { cell }).ToList())
            .ToList();
        return new PdfTableGrid(rows.Count, rows[0].Count, cellTexts);
    }

    private static Letter CreateLetter(string value)
    {
        var fontDetails = new FontDetails("TestFont", isBold: false, weight: 400, isItalic: false);
        return new Letter(
            value,
            new PdfRectangle(0, 0, 10, 10),
            new PdfRectangle(0, 0, 10, 10),
            new PdfPoint(0, 0),
            new PdfPoint(10, 0),
            width: 10,
            fontSize: 10,
            fontDetails,
            TextRenderingMode.Fill,
            GrayColor.Black,
            GrayColor.Black,
            pointSize: 10,
            textSequence: 0);
    }
}
