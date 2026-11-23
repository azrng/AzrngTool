using UglyToad.PdfPig.Content;

namespace AzrngTools.Services.Pdf.MarkdownExport;

/// <summary>已落盘图片的引用：图片块与 md 内的相对引用（按版面顺序排列，序号即列表序）。</summary>
internal sealed record PdfMarkdownImageRef(PdfImageBlock Block, string Reference);

/// <summary>
/// 单页组装输入：版面块、超链接、标题来源（书签坐标与结构标签）、图片相对引用。
/// </summary>
/// <param name="Blocks">该页版面块（按阅读顺序）。</param>
/// <param name="Links">该页超链接（含矩形与目标地址）。</param>
/// <param name="Bookmarks">锚定到该页的大纲书签（层级 0 起算，Top 为距页顶距离，无坐标为 null）。</param>
/// <param name="TaggedHeadings">该页结构标签标题（H1~H6 标签元素的字母集合，按引用匹配）。</param>
/// <param name="Images">已落盘图片的相对引用（忽略图片模式为空表）。</param>
internal sealed record PdfPageComposeInput(
    IReadOnlyList<PdfLayoutBlock> Blocks,
    IReadOnlyList<Hyperlink> Links,
    IReadOnlyList<PdfHeadingBookmark> Bookmarks,
    IReadOnlyList<PdfTaggedHeading> TaggedHeadings,
    IReadOnlyList<PdfMarkdownImageRef> Images);

/// <summary>锚定到某页的大纲书签：层级 0 起算，Top 为目标位置距页顶距离（指向标题行顶边；无坐标为 null，不参与命中）。</summary>
internal sealed record PdfHeadingBookmark(int Level, double? Top);

/// <summary>结构标签标题：H1~H6 标签及其覆盖的字母（PdfPig 对同一字母为同一实例，按引用匹配）。</summary>
internal sealed record PdfTaggedHeading(int Level, IReadOnlyCollection<Letter> Letters);

/// <summary>组装结果：Markdown 源文本与标题行数。</summary>
internal sealed record PdfMarkdownComposeResult(string Markdown, int HeadingCount);

/// <summary>
/// 版面块 → GFM 纯映射器：标题三级判定（书签坐标 → 结构标签 → 字号兜底）、加粗/斜体 run、
/// 超链接、GFM 表格、同段合并；不直接依赖 UI 与文件系统（图片落盘由服务层完成后传入相对引用）。
/// </summary>
internal static class PdfMarkdownComposer
{
    /// <summary>书签 Top 与行 Top 的同轴命中容差（pt）。</summary>
    public const double BookmarkTopTolerance = 8.0;

    /// <summary>图片型判定阈值：平均每页字母数低于该值视为无可提取文本（扫描件/图片型内容）。</summary>
    public const double ImageOnlyLettersPerPageThreshold = 10;

    /// <summary>字号兜底判定：行平均字号需达到正文众数字号的倍数下限。</summary>
    public const double HeadingSizeRatio = 1.35;

    /// <summary>字号兜底判定：行内词字号最大最小差相对行内最大字号的占比上限。</summary>
    public const double HeadingSizeUniformityRatio = 0.30;

    private const double ParagraphMergeLineGapRatio = 0.9;
    private const int HeadingMinLength = 2;
    private const int HeadingMaxLength = 60;
    private const int HeadingMaxWords = 12;

    public static PdfMarkdownComposeResult Compose(IReadOnlyList<PdfPageComposeInput> pages)
    {
        var bodyFontSize = DetectBodyFontSize(pages);
        var sb = new System.Text.StringBuilder();
        var headingCount = 0;

        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = pages[pageIndex];
            if (pageIndex > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<!-- 第 {pageIndex + 1} 页 -->");
                sb.AppendLine();
            }

            string? pendingParagraph = null;
            double pendingTop = 0;
            double pendingHeight = 0;

            var pageLines = page.Blocks.OfType<PdfTextLineBlock>().ToList();
            var bookmarkAnchors = ResolveBookmarkAnchors(pageLines, page.Bookmarks);

            foreach (var block in page.Blocks)
            {
                switch (block)
                {
                    case PdfTextLineBlock line:
                    {
                        var rendered = RenderTextLine(line, page, bodyFontSize, bookmarkAnchors);
                        if (rendered is null)
                        {
                            continue;
                        }

                        var lineHeight = line.Words.Max(word => word.BoundingBox.Height);
                        if (!rendered.Value.IsHeading && pendingParagraph is not null &&
                            line.Top - pendingTop <= Math.Max(pendingHeight, lineHeight) * ParagraphMergeLineGapRatio)
                        {
                            pendingParagraph = JoinParagraph(pendingParagraph, rendered.Value.Text);
                        }
                        else
                        {
                            AppendParagraph(sb, pendingParagraph);
                            pendingParagraph = rendered.Value.Text;
                        }

                        if (rendered.Value.IsHeading)
                        {
                            headingCount++;
                        }

                        pendingTop = line.Top;
                        pendingHeight = lineHeight;
                        break;
                    }

                    case PdfTableBlock table:
                        AppendParagraph(sb, pendingParagraph);
                        sb.AppendLine();
                        sb.AppendLine(RenderTable(table.Grid));
                        sb.AppendLine();
                        break;

                    case PdfImageBlock image:
                        AppendParagraph(sb, pendingParagraph);
                        var imageIndex = -1;
                        for (var imageSlot = 0; imageSlot < page.Images.Count; imageSlot++)
                        {
                            if (page.Images[imageSlot].Block == image)
                            {
                                imageIndex = imageSlot;
                                break;
                            }
                        }

                        if (imageIndex >= 0)
                        {
                            sb.AppendLine();
                            sb.AppendLine($"![图{imageIndex + 1}]({page.Images[imageIndex].Reference})");
                            sb.AppendLine();
                        }
                        break;
                }
            }

            AppendParagraph(sb, pendingParagraph);
        }

        return new PdfMarkdownComposeResult(sb.ToString(), headingCount);
    }

    /// <summary>行高：行内词包围盒最大高（行 Top 为底边距页顶距离，顶边 = Top − 行高）。</summary>
    private static double LineTopEdgeHeight(PdfTextLineBlock line)
    {
        return line.Words.Count > 0 ? line.Words.Max(word => word.BoundingBox.Height) : 0;
    }

    private static void AppendParagraph(System.Text.StringBuilder sb, string? paragraph)    {
        if (paragraph is null)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine(paragraph);
    }

    /// <summary>
    /// 正文字号众数：全文档词字号按 0.5pt 聚桶后取字符数最多的桶（标题判定基准）。
    /// </summary>
    public static double DetectBodyFontSize(IReadOnlyList<PdfPageComposeInput> pages)
    {
        var allWords = new List<PdfLayoutWord>();
        foreach (var page in pages)
        {
            foreach (var block in page.Blocks.OfType<PdfTextLineBlock>())
            {
                allWords.AddRange(block.Words);
            }
        }

        var best = allWords
            .Where(word => word.FontSize > 0)
            .GroupBy(word => Math.Round(word.FontSize * 2) / 2)
            .OrderByDescending(group => group.Sum(word => word.Text.Length))
            .FirstOrDefault();
        return best?.Key ?? 0;
    }

    /// <summary>
    /// 渲染一行文本：标题三级判定命中时输出 # 前缀，否则按加粗/斜体/链接 run 输出；
    /// 空白行返回 null。
    /// </summary>
    public static (string Text, bool IsHeading)? RenderTextLine(
        PdfTextLineBlock line,
        PdfPageComposeInput page,
        double bodyFontSize,
        IReadOnlyDictionary<PdfTextLineBlock, int>? bookmarkAnchors = null)
    {
        var lineText = string.Join(" ", line.Words.Select(word => word.Text));
        if (string.IsNullOrWhiteSpace(lineText))
        {
            return null;
        }

        var level = ResolveHeadingLevel(line, page, bodyFontSize, bookmarkAnchors);
        var content = string.Concat(BuildRuns(line.Words, page.Links));
        return level.HasValue
            ? (new string('#', level.Value) + " " + content, true)
            : (content, false);
    }

    /// <summary>
    /// 书签锚定：书签 Top（距页顶口径，指向标题行顶边）与行顶边（行 Top − 行高）比较，
    /// 每个书签只锚定容差内距离最近的一行，每行最多被一个书签占用。
    /// 直接按行 Top（底边）或原始自页底 y 匹配会系统性错位一个行高、在密集排版页
    /// 把同基线行误标为标题（真实样本实测 142 行误报），必须口径对齐 + 贪心唯一配对。
    /// </summary>
    public static Dictionary<PdfTextLineBlock, int> ResolveBookmarkAnchors(
        IReadOnlyList<PdfTextLineBlock> lines,
        IReadOnlyList<PdfHeadingBookmark> bookmarks)
    {
        var anchors = new Dictionary<PdfTextLineBlock, int>();
        var candidates = new List<(PdfTextLineBlock Line, PdfHeadingBookmark Bookmark, double Distance)>();
        foreach (var line in lines)
        {
            var lineTopEdge = line.Top - LineTopEdgeHeight(line);
            foreach (var bookmark in bookmarks)
            {
                if (bookmark.Top.HasValue)
                {
                    var distance = Math.Abs(bookmark.Top.Value - lineTopEdge);
                    if (distance <= BookmarkTopTolerance)
                    {
                        candidates.Add((line, bookmark, distance));
                    }
                }
            }
        }

        var usedLines = new HashSet<PdfTextLineBlock>();
        var usedBookmarks = new HashSet<PdfHeadingBookmark>();
        foreach (var (line, bookmark, _) in candidates.OrderBy(pair => pair.Distance))
        {
            if (usedLines.Contains(line) || usedBookmarks.Contains(bookmark))
            {
                continue;
            }

            usedLines.Add(line);
            usedBookmarks.Add(bookmark);
            anchors[line] = Math.Clamp(bookmark.Level + 1, 1, 6);
        }

        return anchors;
    }

    /// <summary>
    /// 标题三级判定：书签锚定 → 结构标签字母命中 → 字号兜底（强约束压制行号/上标/装饰字符误报）。
    /// </summary>
    public static int? ResolveHeadingLevel(
        PdfTextLineBlock line,
        PdfPageComposeInput page,
        double bodyFontSize,
        IReadOnlyDictionary<PdfTextLineBlock, int>? bookmarkAnchors = null)
    {
        // 1. 书签锚定命中（书签层级 0 起算，md 标题 1 起算）
        if (bookmarkAnchors is not null && bookmarkAnchors.TryGetValue(line, out var bookmarkLevel))
        {
            return bookmarkLevel;
        }

        // 2. 结构标签命中：按行首词首字母的引用归属（PdfPig 对同一字母为同一实例）
        var firstLetter = line.Words.FirstOrDefault(word => word.FirstLetter is not null)?.FirstLetter;
        if (firstLetter is not null)
        {
            foreach (var tagged in page.TaggedHeadings)
            {
                if (tagged.Letters.Contains(firstLetter))
                {
                    return Math.Clamp(tagged.Level, 1, 6);
                }
            }
        }

        // 3. 字号兜底：强约束（长度、词数、非纯数字符号、行内字号均匀）
        var avgFontSize = line.Words.Where(word => word.FontSize > 0).Select(word => word.FontSize)
            .DefaultIfEmpty(0).Average();
        if (bodyFontSize <= 0 || avgFontSize < bodyFontSize * HeadingSizeRatio)
        {
            return null;
        }

        var lineText = string.Join(" ", line.Words.Select(word => word.Text)).Trim();
        if (lineText.Length < HeadingMinLength || lineText.Length > HeadingMaxLength || line.Words.Count > HeadingMaxWords)
        {
            return null;
        }

        if (!lineText.Any(char.IsLetter))
        {
            return null;
        }

        var fontSizes = line.Words.Where(word => word.FontSize > 0).Select(word => word.FontSize).ToList();
        if (fontSizes.Count > 0 && (fontSizes.Max() - fontSizes.Min()) > fontSizes.Max() * HeadingSizeUniformityRatio)
        {
            return null;
        }

        return avgFontSize >= bodyFontSize * 1.7 ? 1 : avgFontSize >= bodyFontSize * 1.5 ? 2 : 3;
    }

    /// <summary>
    /// 相邻词按（加粗、斜体、链接目标）分组为 run，输出 **粗** / *斜* / [文本](地址)。
    /// </summary>
    public static List<string> BuildRuns(IReadOnlyList<PdfLayoutWord> words, IReadOnlyList<Hyperlink> links)
    {
        var runs = new List<(bool Bold, bool Italic, string? Uri, System.Text.StringBuilder Text)>();
        foreach (var word in words)
        {
            var uri = FindLinkUri(word, links);
            var text = EscapeInline(word.Text);
            if (runs.Count > 0 &&
                runs[^1].Bold == word.IsBold &&
                runs[^1].Italic == word.IsItalic &&
                runs[^1].Uri == uri)
            {
                var joinCjk = IsCjk(runs[^1].Text[^1]) || (!string.IsNullOrEmpty(word.Text) && IsCjk(word.Text[0]));
                runs[^1].Text.Append(joinCjk ? text : " " + text);
            }
            else
            {
                runs.Add((word.IsBold, word.IsItalic, uri, new System.Text.StringBuilder(text)));
            }
        }

        var result = new List<string>(runs.Count);
        foreach (var (bold, italic, uri, text) in runs)
        {
            var content = text.ToString();
            if (uri is not null)
            {
                result.Add($"[{content}]({FormatUri(uri)})");
            }
            else if (italic)
            {
                result.Add($"*{content}*");
            }
            else if (bold)
            {
                result.Add($"**{content}**");
            }
            else
            {
                result.Add(content);
            }
        }

        return result;
    }

    /// <summary>词中心点命中的超链接地址；词中心未落在任何链接矩形内返回 null。</summary>
    public static string? FindLinkUri(PdfLayoutWord word, IReadOnlyList<Hyperlink> links)
    {
        var box = word.BoundingBox;
        var (centerX, centerY) = ((box.Left + box.Right) / 2, (box.Bottom + box.Top) / 2);
        foreach (var link in links)
        {
            var bounds = link.Bounds;
            if (centerX >= bounds.Left && centerX <= bounds.Right &&
                centerY >= bounds.Bottom && centerY <= bounds.Top &&
                !string.IsNullOrWhiteSpace(link.Uri))
            {
                return link.Uri;
            }
        }

        return null;
    }

    /// <summary>
    /// 同段合并：中文相邻字符间不加空格；标点、连字符等衔接处同样不加。
    /// </summary>
    public static string JoinParagraph(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return left + right;
        }

        var last = left[^1];
        var first = right[0];
        var noSpace = IsCjk(last) || IsCjk(first) ||
            last is '-' or '/' or '（' or '(' || first is '.' or ',' or ')' or '）' or '%' or '。' or '，';
        return noSpace ? left + right : left + " " + right;
    }

    /// <summary>有线表格网格 → GFM 表格（首行表头 + 分隔行，竖线转义、单元格内多行合并）。</summary>
    public static string RenderTable(PdfTableGrid grid)
    {
        var lines = new List<string>(grid.Rows + 1);
        for (var row = 0; row < grid.Rows; row++)
        {
            var cells = new string[grid.Columns];
            for (var column = 0; column < grid.Columns; column++)
            {
                cells[column] = EscapeTableCell(string.Join(" ", grid.CellTexts[row][column]));
            }

            lines.Add("| " + string.Join(" | ", cells) + " |");
            if (row == 0)
            {
                lines.Add("|" + string.Join("|", Enumerable.Range(0, grid.Columns).Select(_ => " --- ")) + "|");
            }
        }

        return string.Join("\n", lines);
    }

    private static string EscapeTableCell(string text)
    {
        return text.Replace("|", "\\|").Replace("\n", " ").Trim();
    }

    private static string EscapeInline(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("`", "\\`");
    }

    private static string FormatUri(string uri)
    {
        return uri.Contains(' ') || uri.Contains('(') || uri.Contains(')') ? $"<{uri}>" : uri;
    }

    private static bool IsCjk(char c)
    {
        return c is (>= (char)0x2E80 and <= (char)0x9FFF)
            or (>= (char)0xF900 and <= (char)0xFAFF)
            or (>= (char)0xFF00 and <= (char)0xFFEF);
    }
}
