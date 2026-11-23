using AzrngTools.Utils;
// 模块命名空间 AzrngTools.Models.Word/Services.Word 会遮蔽 PdfPig 的 Content.Word 类型，别名显式还原
using PdfWord = UglyToad.PdfPig.Content.Word;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.ReadingOrderDetector;

namespace AzrngTools.Services.Pdf;

/// <summary>
/// Word 导出的词级版面信息：字号 / 加粗 / 位置，供写出时还原。
/// </summary>
internal sealed record PdfLayoutWord(string Text, bool IsBold, double FontSize, PdfRectangle BoundingBox);

/// <summary>
/// Word 导出的版面块：段落行、有线表格或图片，统一按 Top（距页顶距离）升序写出。
/// </summary>
internal abstract record PdfLayoutBlock(double Top);

internal sealed record PdfTextLineBlock(IReadOnlyList<PdfLayoutWord> Words, double Top) : PdfLayoutBlock(Top);

internal sealed record PdfImageBlock(byte[] JpegBytes, double DisplayWidthPt, double DisplayHeightPt, double Top) : PdfLayoutBlock(Top);

internal sealed record PdfTableBlock(PdfTableGrid Grid, double Top) : PdfLayoutBlock(Top);

/// <summary>
/// 有线表格网格：行自上而下、列自左向右；单元格内容为按行拆分的文本（可能多行）。
/// </summary>
internal sealed record PdfTableGrid(int Rows, int Columns, IReadOnlyList<IReadOnlyList<IReadOnlyList<string>>> CellTexts);

/// <summary>
/// PDF 页面版面分析：把词、表格线与图片整理成版面块，文本行按阅读顺序排列。
/// 只依赖 PdfPig 公开 API（直线段通过子路径包围盒退化识别，线段命令本身为私有类型无法直接读取）。
/// </summary>
internal static class PdfPageLayoutAnalyzer
{
    /// <summary>词聚类为行的基线容差，与既有 Word 导出行为一致。</summary>
    private const double LineTolerance = 3.0;

    /// <summary>包围盒高 / 宽小于该值视为直线段（描边线或填充细矩形）。</summary>
    private const double LineThickness = 1.5;

    /// <summary>短于该长度的线段不参与表格识别（排除下划线、短装饰线）。</summary>
    private const double MinRuleLength = 20.0;

    /// <summary>表格线位置聚类容差。</summary>
    private const double RuleClusterTolerance = 2.0;

    public static IReadOnlyList<PdfLayoutBlock> Analyze(Page page)
    {
        var rawWords = page.GetWords().ToList();
        var words = rawWords.Select(ToLayoutWord).ToList();
        var tables = DetectTables(page, words);
        var blocks = new List<PdfLayoutBlock>();

        foreach (var table in tables)
        {
            blocks.Add(new PdfTableBlock(table.Grid, table.Top));
        }

        // 归入表格的词不再进入段落行
        var consumed = tables
            .SelectMany(table => table.CellWords.SelectMany(row => row))
            .SelectMany(cellWords => cellWords)
            .ToHashSet();

        var textLines = SegmentIntoReadingOrderLines(page, rawWords, words, consumed);
        blocks.AddRange(textLines);
        blocks.AddRange(ExtractImages(page));

        return MergeBlocksByReadingOrder(blocks, textLines);
    }

    /// <summary>
    /// 纯文本降级路径：跳过表格与图片识别，仅做行聚类（供单页分析异常时兜底）。
    /// </summary>
    public static IReadOnlyList<PdfLayoutBlock> AnalyzeTextOnly(Page page)
    {
        var words = page.GetWords().Select(ToLayoutWord).ToList();
        return BuildTextLineBlocks(page, words).ToList();
    }

    /// <summary>
    /// 栏感知文本分块：RecursiveXYCut 递归切分避免双栏/多栏同基线词并成一行，
    /// UnsupervisedReadingOrderDetector 输出按栏阅读顺序；失败时回退既有基线聚类平铺路径（单栏行为）。
    /// </summary>
    private static List<PdfTextLineBlock> SegmentIntoReadingOrderLines(
        Page page,
        List<PdfWord> rawWords,
        List<PdfLayoutWord> layoutWords,
        HashSet<PdfLayoutWord> consumed)
    {
        var layoutByRaw = new Dictionary<PdfWord, PdfLayoutWord>(rawWords.Count);
        var remaining = new List<PdfLayoutWord>(rawWords.Count);
        for (var i = 0; i < rawWords.Count; i++)
        {
            layoutByRaw[rawWords[i]] = layoutWords[i];
            if (!consumed.Contains(layoutWords[i]))
            {
                remaining.Add(layoutWords[i]);
            }
        }

        if (remaining.Count == 0)
        {
            return [];
        }

        try
        {
            var segmenter = new RecursiveXYCut(new RecursiveXYCut.RecursiveXYCutOptions
            {
                // 块最小宽度取页宽 1/10：该值含义是"投影块达到此宽度才允许其后切分"，
                // 官方双栏用例的页宽 1/3 只覆盖半页宽栏；窄栏（侧栏/简报 60pt 级）会被漏切回串行。
                // 切分过细无害——块随后由 UnsupervisedReadingOrderDetector 按栏序重排，漏切才是致命场景。
                MinimumWidth = page.Width / 10.0,
            });
            var orderedBlocks = UnsupervisedReadingOrderDetector.Instance
                .Get(segmenter.GetBlocks(rawWords.Where(raw => !consumed.Contains(layoutByRaw[raw]))))
                .ToList();

            var lines = new List<PdfTextLineBlock>();
            foreach (var block in orderedBlocks)
            {
                foreach (var line in block.TextLines)
                {
                    var lineWords = new List<PdfLayoutWord>();
                    foreach (var raw in line.Words)
                    {
                        if (layoutByRaw.TryGetValue(raw, out var layout))
                        {
                            lineWords.Add(layout);
                        }
                    }

                    if (lineWords.Count == 0)
                    {
                        continue;
                    }

                    SortLineLeftToRight(lineWords);
                    // PDF y 轴向上：行底边 y 越大越靠页面上方
                    var lineBottom = lineWords.Max(word => word.BoundingBox.Bottom);
                    lines.Add(new PdfTextLineBlock(lineWords, page.Height - lineBottom));
                }
            }

            return lines;
        }
        catch (Exception ex)
        {
            AppLog.LogError($"PDF 版面分栏分析失败，回退单栏行聚类: {ex.Message}\n{ex.GetExceptionAndStack()}");
            return BuildTextLineBlocks(page, remaining).ToList();
        }
    }

    /// <summary>
    /// 文本行按阅读顺序作骨架；表格与图片按 Top 几何插入骨架（首个 Top 更大的文本行之前，无则追加末尾），
    /// 避免整体按 Top 排序把分栏阅读顺序重新打散。
    /// </summary>
    private static IReadOnlyList<PdfLayoutBlock> MergeBlocksByReadingOrder(
        List<PdfLayoutBlock> blocks,
        List<PdfTextLineBlock> textLines)
    {
        if (textLines.Count == 0)
        {
            return blocks.OrderBy(block => block.Top).ToList();
        }

        var result = new List<PdfLayoutBlock>(textLines);
        foreach (var block in blocks.Where(candidate => candidate is not PdfTextLineBlock).OrderBy(candidate => candidate.Top))
        {
            var insertIndex = result.FindIndex(existing => existing is PdfTextLineBlock line && line.Top > block.Top);
            if (insertIndex >= 0)
            {
                result.Insert(insertIndex, block);
            }
            else
            {
                result.Add(block);
            }
        }

        return result;
    }

    private static PdfLayoutWord ToLayoutWord(PdfWord word)
    {
        var letters = word.Letters;
        var firstLetter = letters.Count > 0 ? letters[0] : null;
        return new PdfLayoutWord(
            word.Text,
            firstLetter?.FontDetails?.IsBold == true,
            firstLetter?.FontSize ?? 0,
            word.BoundingBox);
    }

    private static IEnumerable<PdfTextLineBlock> BuildTextLineBlocks(Page page, List<PdfLayoutWord> words)
    {
        if (words.Count == 0)
        {
            yield break;
        }

        foreach (var line in GroupIntoLines(words))
        {
            // PDF y 轴向上：行底边 y 越大越靠页面上方
            var lineBottom = line.Max(word => word.BoundingBox.Bottom);
            yield return new PdfTextLineBlock(line, page.Height - lineBottom);
        }
    }

    /// <summary>按基线聚类成行（自上而下），行内自左向右；与既有导出分组逻辑一致。</summary>
    private static List<List<PdfLayoutWord>> GroupIntoLines(List<PdfLayoutWord> words)
    {
        var lines = new List<List<PdfLayoutWord>>();
        if (words.Count == 0)
        {
            return lines;
        }

        var sorted = words
            .OrderByDescending(word => word.BoundingBox.Bottom)
            .ThenBy(word => word.BoundingBox.Left)
            .ToList();

        var currentLine = new List<PdfLayoutWord> { sorted[0] };
        var currentY = sorted[0].BoundingBox.Bottom;

        for (var i = 1; i < sorted.Count; i++)
        {
            var word = sorted[i];
            if (Math.Abs(word.BoundingBox.Bottom - currentY) > LineTolerance)
            {
                SortLineLeftToRight(currentLine);
                lines.Add(currentLine);
                currentLine = new List<PdfLayoutWord>();
                currentY = word.BoundingBox.Bottom;
            }

            currentLine.Add(word);
        }

        if (currentLine.Count > 0)
        {
            SortLineLeftToRight(currentLine);
            lines.Add(currentLine);
        }

        return lines;
    }

    private static void SortLineLeftToRight(List<PdfLayoutWord> line)
    {
        line.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
    }

    #region 有线表格识别

    private sealed record DetectedTable(PdfTableGrid Grid, double Top, IReadOnlyList<IReadOnlyList<List<PdfLayoutWord>>> CellWords);

    private sealed record RuledGrid(int Rows, int Columns, IReadOnlyList<double> RowBoundaries, IReadOnlyList<double> ColumnBoundaries, PdfRectangle Bounds, double Top);

    /// <summary>
    /// 识别由正交直线围出的表格：至少围出两个格子才算表格（2×3 或 3×2），
    /// 避免下划线或单格边框误判；单元格文本按词中心点归位。
    /// </summary>
    private static List<DetectedTable> DetectTables(Page page, List<PdfLayoutWord> words)
    {
        var grids = FindRuledGrids(page);
        if (grids.Count == 0)
        {
            return [];
        }

        var cellWords = grids.Select(grid => Enumerable.Range(0, grid.Rows)
            .Select(_ => Enumerable.Range(0, grid.Columns).Select(_ => new List<PdfLayoutWord>()).ToList())
            .ToList()).ToList();

        foreach (var word in words)
        {
            var box = word.BoundingBox;
            var (centerX, centerY) = ((box.Left + box.Right) / 2, (box.Bottom + box.Top) / 2);
            for (var gridIndex = 0; gridIndex < grids.Count; gridIndex++)
            {
                var grid = grids[gridIndex];
                if (centerX < grid.Bounds.Left || centerX > grid.Bounds.Right ||
                    centerY < grid.Bounds.Bottom || centerY > grid.Bounds.Top)
                {
                    continue;
                }

                var row = FindSegmentIndex(grid.RowBoundaries, centerY);
                var column = FindSegmentIndex(grid.ColumnBoundaries, centerX);
                if (row >= 0 && column >= 0)
                {
                    cellWords[gridIndex][row][column].Add(word);
                }

                break;
            }
        }

        var tables = new List<DetectedTable>(grids.Count);
        for (var gridIndex = 0; gridIndex < grids.Count; gridIndex++)
        {
            var grid = grids[gridIndex];
            var cellTexts = new List<IReadOnlyList<IReadOnlyList<string>>>(grid.Rows);
            for (var row = 0; row < grid.Rows; row++)
            {
                var rowTexts = new List<IReadOnlyList<string>>(grid.Columns);
                for (var column = 0; column < grid.Columns; column++)
                {
                    var lines = GroupIntoLines(cellWords[gridIndex][row][column]);
                    rowTexts.Add(lines.Select(line => string.Join(" ", line.Select(word => word.Text))).ToList());
                }

                cellTexts.Add(rowTexts);
            }

            tables.Add(new DetectedTable(
                new PdfTableGrid(grid.Rows, grid.Columns, cellTexts),
                grid.Top,
                cellWords[gridIndex]));
        }

        return tables;
    }

    private static int FindSegmentIndex(IReadOnlyList<double> boundaries, double value)
    {
        // 相邻格线围出区间即一个格子；行边界为降序、列边界为升序，故按无序区间判断
        for (var i = 0; i < boundaries.Count - 1; i++)
        {
            var first = boundaries[i];
            var second = boundaries[i + 1];
            if (value >= Math.Min(first, second) && value <= Math.Max(first, second))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>把描边直线段聚类成正交网格；PDF y 轴向上，行边界按 y 降序（首行为最上方）。</summary>
    private static List<RuledGrid> FindRuledGrids(Page page)
    {
        var horizontal = new List<(double Position, double From, double To)>();
        var vertical = new List<(double Position, double From, double To)>();

        foreach (var path in page.Paths)
        {
            if (!path.IsStroked)
            {
                continue;
            }

            foreach (var subpath in path)
            {
                var bounds = subpath.GetBoundingRectangle();
                if (!bounds.HasValue)
                {
                    continue;
                }

                var rect = bounds.Value;
                if (rect.Height <= LineThickness && rect.Width >= MinRuleLength)
                {
                    horizontal.Add((rect.Bottom, rect.Left, rect.Right));
                }
                else if (rect.Width <= LineThickness && rect.Height >= MinRuleLength)
                {
                    vertical.Add((rect.Left, rect.Bottom, rect.Top));
                }
            }
        }

        if (horizontal.Count < 2 || vertical.Count < 2)
        {
            return [];
        }

        var horizontalRules = ClusterRules(horizontal);
        var verticalRules = ClusterRules(vertical);

        // 至少围出两个格子（2×3 或 3×2）才算表格，避免单格边框误判
        if ((horizontalRules.Count - 1) * (verticalRules.Count - 1) < 2)
        {
            return [];
        }

        var left = Math.Min(horizontal.Min(line => line.From), vertical.Min(line => line.Position));
        var right = Math.Max(horizontal.Max(line => line.To), vertical.Max(line => line.Position));
        var bottom = Math.Min(vertical.Min(line => line.From), horizontal.Min(line => line.Position));
        var top = Math.Max(vertical.Max(line => line.To), horizontal.Max(line => line.Position));

        return
        [
            new RuledGrid(
                horizontalRules.Count - 1,
                verticalRules.Count - 1,
                horizontalRules.Select(rule => rule.Position).OrderByDescending(position => position).ToList(),
                verticalRules.Select(rule => rule.Position).OrderBy(position => position).ToList(),
                new PdfRectangle(left, bottom, right, top),
                page.Height - top)
        ];
    }

    private static List<(double Position, double From, double To)> ClusterRules(List<(double Position, double From, double To)> rules)
    {
        var ordered = rules.OrderBy(rule => rule.Position).ToList();
        var clusters = new List<(double PositionSum, int Count, double From, double To)>();
        foreach (var rule in ordered)
        {
            if (clusters.Count > 0 && rule.Position - clusters[^1].PositionSum / clusters[^1].Count <= RuleClusterTolerance)
            {
                var last = clusters[^1];
                clusters[^1] = (last.PositionSum + rule.Position, last.Count + 1, Math.Min(last.From, rule.From), Math.Max(last.To, rule.To));
            }
            else
            {
                clusters.Add((rule.Position, 1, rule.From, rule.To));
            }
        }

        return clusters.Select(cluster => (cluster.PositionSum / cluster.Count, cluster.From, cluster.To)).ToList();
    }

    #endregion

    #region 图片提取

    /// <summary>
    /// 提取页面位图图片：仅嵌入 JPEG（DCTDecode 直通流，FFD8 头），
    /// 其余滤镜（Flate 原始像素、JPX、CCITT）无法可靠解码，跳过。
    /// </summary>
    private static IEnumerable<PdfImageBlock> ExtractImages(Page page)
    {
        foreach (var image in page.GetImages())
        {
            var raw = image.RawMemory;
            if (raw.Length < 3 || raw.Span[0] != 0xFF || raw.Span[1] != 0xD8)
            {
                continue;
            }

            var bounds = image.BoundingBox;
            yield return new PdfImageBlock(
                raw.ToArray(),
                bounds.Width,
                bounds.Height,
                page.Height - bounds.Top);
        }
    }

    #endregion
}
