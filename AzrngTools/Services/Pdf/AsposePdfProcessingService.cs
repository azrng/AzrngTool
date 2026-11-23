using Azrng.Core.Helpers;
using Azrng.Core.Results;
using AzrngTools.Models.Pdf;
using AzrngTools.Services.Pdf.MarkdownExport;
using AzrngTools.Utils;
using NPOI.XWPF.UserModel;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Outline.Destinations;

namespace AzrngTools.Services.Pdf;

public sealed class AsposePdfProcessingService : IPdfProcessingService, ITransientDependency
{
    public string LicenseStatusText => "使用开源组件（PdfPig + PDFsharp + NPOI），无需授权";

    public bool IsEvaluationMode => false;

    public Task<IResultModel<PdfLoadedFileSummary>> LoadMetadataAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationError = ValidateSourcePdf(filePath);
                if (validationError is not null)
                {
                    return PdfResultModel.Failure<PdfLoadedFileSummary>(validationError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                using var loadPerf = PerfLogScope.Start(
                    "PDF元数据加载",
                    $"{Path.GetFileName(filePath)} {PerfLogScope.DescribeSize(new FileInfo(filePath).Length)}");
                using var document = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
                var fileInfo = new FileInfo(filePath);
                var summary = new PdfLoadedFileSummary(
                    filePath,
                    fileInfo.Name,
                    fileInfo.Length,
                    document.PageCount,
                    IsEvaluationMode,
                    LicenseStatusText);

                return PdfResultModel.Success(summary, $"已加载 PDF，共 {summary.PageCount} 页");
            }
            catch (OperationCanceledException)
            {
                return PdfResultModel.Failure<PdfLoadedFileSummary>("PDF 加载已取消");
            }
            catch (Exception ex)
            {
                var friendlyMessage = BuildFriendlyError("PDF 加载失败", ex);
                AppLog.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return PdfResultModel.Failure<PdfLoadedFileSummary>(friendlyMessage);
            }
        }, cancellationToken);
    }

    public Task<IResultModel<PdfSplitResult>> SplitAsync(
        PdfSplitRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationError = ValidateSplitRequest(request);
                if (validationError is not null)
                {
                    return PdfResultModel.Failure<PdfSplitResult>(validationError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                using var splitPerf = PerfLogScope.Start(
                    "PDF分割",
                    $"{request.Pages.Count} 页 → {(request.Mode == PdfSplitMode.IndividualPages ? "逐页多文件" : "单文件")}");
                using var source = PdfReader.Open(request.SourceFilePath, PdfDocumentOpenMode.Import);
                ValidatePagesWithinDocument(request.Pages, source.PageCount);

                var outputFiles = request.Mode == PdfSplitMode.IndividualPages
                    ? SplitIndividualPages(source, request.Pages, request.OutputPath, cancellationToken)
                    : SplitToSingleFile(source, request.Pages, request.OutputPath, cancellationToken);

                var result = new PdfSplitResult(outputFiles, IsEvaluationMode);
                var message = request.Mode == PdfSplitMode.IndividualPages
                    ? $"PDF 分割完成，共导出 {result.OutputFileCount} 个文件"
                    : $"PDF 分割完成：{outputFiles[0]}";

                return PdfResultModel.Success(result, message);
            }
            catch (OperationCanceledException)
            {
                return PdfResultModel.Failure<PdfSplitResult>("PDF 分割已取消");
            }
            catch (Exception ex)
            {
                var friendlyMessage = BuildFriendlyError("PDF 分割失败", ex);
                AppLog.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return PdfResultModel.Failure<PdfSplitResult>(friendlyMessage);
            }
        }, cancellationToken);
    }

    public Task<IResultModel<PdfWordExportResult>> ExportWordAsync(
        PdfWordExportRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationError = ValidateWordExportRequest(request);
                if (validationError is not null)
                {
                    return PdfResultModel.Failure<PdfWordExportResult>(validationError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                EnsureOutputDirectory(request.OutputFilePath);
                ExportPdfToWord(request.SourceFilePath, request.OutputFilePath, cancellationToken);

                var result = new PdfWordExportResult(request.OutputFilePath, IsEvaluationMode);
                return PdfResultModel.Success(result, $"Word 导出完成：{request.OutputFilePath}");
            }
            catch (OperationCanceledException)
            {
                return PdfResultModel.Failure<PdfWordExportResult>("Word 导出已取消");
            }
            catch (Exception ex)
            {
                var friendlyMessage = BuildFriendlyError("Word 导出失败", ex);
                AppLog.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return PdfResultModel.Failure<PdfWordExportResult>(friendlyMessage);
            }
        }, cancellationToken);
    }

    public Task<IResultModel<PdfMarkdownExportResult>> ExportMarkdownAsync(
        PdfMarkdownExportRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationError = ValidateMarkdownExportRequest(request);
                if (validationError is not null)
                {
                    return PdfResultModel.Failure<PdfMarkdownExportResult>(validationError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                EnsureOutputDirectory(request.OutputFilePath);
                return ExportPdfToMarkdown(request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return PdfResultModel.Failure<PdfMarkdownExportResult>("Markdown 导出已取消");
            }
            catch (Exception ex)
            {
                var friendlyMessage = BuildFriendlyError("Markdown 导出失败", ex);
                AppLog.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return PdfResultModel.Failure<PdfMarkdownExportResult>(friendlyMessage);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Markdown 导出内核：图片型判定、逐页版面分析、图片落盘与组装写出。
    /// </summary>
    private static IResultModel<PdfMarkdownExportResult> ExportPdfToMarkdown(
        PdfMarkdownExportRequest request,
        CancellationToken cancellationToken)
    {
        // 版面分析（表格线聚类、图片提取）在复杂真实文档上耗时未知，整链路计时留档
        using var exportPerf = PerfLogScope.Start("PDF导出Markdown", Path.GetFileName(request.SourceFilePath));
        var assetsDirectoryName = $"{Path.GetFileNameWithoutExtension(request.OutputFilePath)}.assets";
        var outputDirectory = Path.GetDirectoryName(request.OutputFilePath) ?? string.Empty;
        var assetsDirectory = Path.Combine(outputDirectory, assetsDirectoryName);

        using var pdf = UglyToad.PdfPig.PdfDocument.Open(request.SourceFilePath);

        // 图片型判定：平均每页字母数低于阈值视为扫描件/图片型内容，转换前拒绝、不产生任何输出
        var letterCount = 0;
        for (var pageNumber = 1; pageNumber <= pdf.NumberOfPages; pageNumber++)
        {
            letterCount += pdf.GetPage(pageNumber).Letters.Count;
        }

        if (pdf.NumberOfPages > 0 &&
            letterCount < pdf.NumberOfPages * PdfMarkdownComposer.ImageOnlyLettersPerPageThreshold)
        {
            return PdfResultModel.Failure<PdfMarkdownExportResult>("该 PDF 无可提取文本（图片型/扫描件内容），暂不支持转换为 Markdown");
        }

        var bookmarksByPage = CollectHeadingBookmarks(pdf);
        var pageInputs = new List<PdfPageComposeInput>(pdf.NumberOfPages);
        var exportedImageCount = 0;

        for (var pageNumber = 1; pageNumber <= pdf.NumberOfPages; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = pdf.GetPage(pageNumber);

            IReadOnlyList<PdfLayoutBlock> blocks;
            try
            {
                blocks = PdfPageLayoutAnalyzer.Analyze(page);
            }
            catch (Exception ex)
            {
                // 单页版面分析失败仅降级该页为纯文本，导出整体不失败
                AppLog.LogError($"Markdown 导出第 {pageNumber} 页版面分析失败，按纯文本处理: {ex.Message}\n{ex.GetExceptionAndStack()}");
                blocks = PdfPageLayoutAnalyzer.AnalyzeTextOnly(page);
            }

            var imageRefs = new List<PdfMarkdownImageRef>();
            if (request.ExportImages)
            {
                foreach (var block in blocks.OfType<PdfImageBlock>())
                {
                    var extension = block.Format == PdfImageFormat.Png ? "png" : "jpg";
                    var fileName = $"img-{++exportedImageCount}.{extension}";
                    // DocSharp 口径一致：只在确实有图片落盘时才创建 assets 目录
                    if (!Directory.Exists(assetsDirectory))
                    {
                        Directory.CreateDirectory(assetsDirectory);
                    }

                    File.WriteAllBytes(Path.Combine(assetsDirectory, fileName), block.Bytes);
                    imageRefs.Add(new PdfMarkdownImageRef(block, $"{assetsDirectoryName}/{fileName}"));
                }
            }

            // 书签 Top 是 PDF 用户空间 y（自页底），版面行 Top 是距页顶距离，先统一口径再锚定
            var pageBookmarks = bookmarksByPage.GetValueOrDefault(pageNumber) ?? [];
            var convertedBookmarks = pageBookmarks
                .Select(bookmark => bookmark with { Top = bookmark.Top.HasValue ? page.Height - bookmark.Top.Value : null })
                .ToList();

            pageInputs.Add(new PdfPageComposeInput(
                blocks,
                page.GetHyperlinks().ToList(),
                convertedBookmarks,
                CollectTaggedHeadings(page),
                imageRefs));
        }

        var composeResult = PdfMarkdownComposer.Compose(pageInputs);
        File.WriteAllText(request.OutputFilePath, composeResult.Markdown);

        var result = new PdfMarkdownExportResult(request.OutputFilePath, composeResult.HeadingCount, exportedImageCount);
        return PdfResultModel.Success(
            result,
            $"Markdown 导出完成：{request.OutputFilePath}（标题 {result.HeadingCount} 个，图片 {result.ExportedImageCount} 张）");
    }

    /// <summary>
    /// 收集大纲书签并按页锚定：层级 0 起算，Top 取目标纵坐标原始值（PDF 用户空间自页底 y，
    /// 交由逐页处理转换成距页顶口径）；无坐标书签不参与标题命中，落到结构标签或字号判定。
    /// </summary>
    private static Dictionary<int, List<PdfHeadingBookmark>> CollectHeadingBookmarks(UglyToad.PdfPig.PdfDocument pdf)
    {
        var result = new Dictionary<int, List<PdfHeadingBookmark>>();
        if (!pdf.TryGetBookmarks(out var bookmarks))
        {
            return result;
        }

        void Walk(IEnumerable<BookmarkNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is DocumentBookmarkNode documentNode && documentNode.PageNumber >= 1)
                {
                    double? top = null;
                    var destination = documentNode.Destination;
                    if (destination is not null && !destination.Coordinates.Equals(ExplicitDestinationCoordinates.Empty))
                    {
                        top = destination.Coordinates.Top;
                    }

                    if (!result.TryGetValue(documentNode.PageNumber, out var list))
                    {
                        list = [];
                        result[documentNode.PageNumber] = list;
                    }

                    list.Add(new PdfHeadingBookmark(node.Level, top));
                }

                if (node.Children.Count > 0)
                {
                    Walk(node.Children);
                }
            }
        }

        Walk(bookmarks.Roots);
        return result;
    }

    /// <summary>
    /// 收集页面 H1~H6 结构标签标题：递归展开标记内容树并聚合后代字母供行首字母引用匹配。
    /// PdfPig 的 AddLetter 只写入最内层 BMC，结构标签正文常在内层子元素中，仅用元素自带 Letters 会漏配。
    /// </summary>
    private static List<PdfTaggedHeading> CollectTaggedHeadings(Page page)
    {
        var headings = new List<PdfTaggedHeading>();

        List<Letter> Walk(IReadOnlyList<MarkedContentElement> elements)
        {
            var letters = new List<Letter>();
            foreach (var element in elements)
            {
                if (element.IsArtifact)
                {
                    // artifact（页眉页脚等装饰内容）不参与标题归属
                    continue;
                }

                var ownLetters = element.Letters.ToList();
                var childLetters = Walk(element.Children);
                letters.AddRange(ownLetters);
                letters.AddRange(childLetters);

                if (element.Tag.Length == 2 && element.Tag[0] == 'H' && char.IsAsciiDigit(element.Tag[1]))
                {
                    headings.Add(new PdfTaggedHeading(element.Tag[1] - '0', ownLetters.Concat(childLetters).ToList()));
                }
            }

            return letters;
        }

        Walk(page.GetMarkedContents());
        return headings;
    }

    private static string? ValidateMarkdownExportRequest(PdfMarkdownExportRequest request)
    {
        var sourceError = ValidateSourcePdf(request.SourceFilePath);
        if (sourceError is not null)
        {
            return sourceError;
        }

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return "请选择 Markdown 输出文件";
        }

        if (!string.Equals(Path.GetExtension(request.OutputFilePath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return "Markdown 输出文件扩展名必须为 .md";
        }

        return null;
    }

    private static void ExportPdfToWord(string pdfPath, string outputPath, CancellationToken cancellationToken)
    {
        // 版面分析（表格线聚类、图片提取）在复杂真实文档上耗时未知，整链路计时留档
        using var exportPerf = PerfLogScope.Start("PDF导出Word", Path.GetFileName(pdfPath));
        using var doc = new XWPFDocument();

        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath))
        {
            for (var pageNum = 1; pageNum <= pdf.NumberOfPages; pageNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = pdf.GetPage(pageNum);

                IReadOnlyList<PdfLayoutBlock> blocks;
                try
                {
                    blocks = PdfPageLayoutAnalyzer.Analyze(page);
                }
                catch (Exception ex)
                {
                    // 单页版面分析失败仅降级该页为纯文本，导出整体不失败
                    AppLog.LogError($"Word 导出第 {pageNum} 页版面分析失败，按纯文本处理: {ex.Message}\n{ex.GetExceptionAndStack()}");
                    blocks = PdfPageLayoutAnalyzer.AnalyzeTextOnly(page);
                }

                if (blocks.Count == 0)
                {
                    continue;
                }

                if (pageNum > 1)
                {
                    var breakRun = doc.CreateParagraph().CreateRun();
                    breakRun.AddBreak(BreakType.PAGE);
                }

                foreach (var block in blocks)
                {
                    switch (block)
                    {
                        case PdfTextLineBlock textLine:
                            WriteTextLine(doc, textLine);
                            break;
                        case PdfTableBlock tableBlock:
                            WriteTable(doc, tableBlock);
                            break;
                        case PdfImageBlock imageBlock:
                            WriteImage(doc, imageBlock);
                            break;
                    }
                }
            }
        }

        using var output = File.Create(outputPath);
        doc.Write(output);
    }

    /// <summary>
    /// 段落行写出：相邻词按（加粗、字号）分组为 run，还原视觉样式；组间以空格衔接。
    /// </summary>
    private static void WriteTextLine(XWPFDocument doc, PdfTextLineBlock line)
    {
        var lineText = string.Join(" ", line.Words.Select(word => word.Text));
        if (string.IsNullOrWhiteSpace(lineText))
        {
            return;
        }

        var groups = new List<(bool IsBold, int FontSize, string Text)>();
        foreach (var word in line.Words)
        {
            var size = word.FontSize > 0 ? (int)Math.Round(word.FontSize) : 0;
            var text = SanitizeXmlText(word.Text);
            if (groups.Count > 0 && groups[^1].IsBold == word.IsBold && groups[^1].FontSize == size)
            {
                groups[^1] = (word.IsBold, size, groups[^1].Text + " " + text);
            }
            else
            {
                groups.Add((word.IsBold, size, text));
            }
        }

        var paragraph = doc.CreateParagraph();
        foreach (var group in groups)
        {
            var run = paragraph.CreateRun();
            run.SetText(group.Text);
            if (group.FontSize > 0)
            {
                // NPOI FontSize 单位为磅（内部换算半磅写入 XML），PdfPig 字号也是磅
                run.FontSize = group.FontSize;
            }

            if (group.IsBold)
            {
                run.IsBold = true;
            }
        }
    }

    private static void WriteTable(XWPFDocument doc, PdfTableBlock tableBlock)
    {
        var grid = tableBlock.Grid;
        var table = doc.CreateTable(grid.Rows, grid.Columns);
        ApplyTableBorders(table);

        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                var cell = table.GetRow(row).GetCell(column);
                var lines = grid.CellTexts[row][column];
                if (lines.Count == 0)
                {
                    continue;
                }

                // 新建单元格自带一个空段落，首行复用，其余行追加段落
                var isFirstLine = true;
                foreach (var lineText in lines)
                {
                    var paragraph = isFirstLine
                        ? (XWPFParagraph)cell.Paragraphs[0]
                        : cell.AddParagraph();
                    isFirstLine = false;
                    paragraph.CreateRun().SetText(SanitizeXmlText(lineText));
                }
            }
        }
    }

    /// <summary>
    /// 有线表格还原为带单线边框的 Word 表格（0.5pt 黑色，size 单位为八分之一磅）。
    /// </summary>
    private static void ApplyTableBorders(XWPFTable table)
    {
        table.SetTopBorder(XWPFTable.XWPFBorderType.SINGLE, 4, 0, "000000");
        table.SetBottomBorder(XWPFTable.XWPFBorderType.SINGLE, 4, 0, "000000");
        table.SetLeftBorder(XWPFTable.XWPFBorderType.SINGLE, 4, 0, "000000");
        table.SetRightBorder(XWPFTable.XWPFBorderType.SINGLE, 4, 0, "000000");
        table.SetInsideHBorder(XWPFTable.XWPFBorderType.SINGLE, 4, 0, "000000");
        table.SetInsideVBorder(XWPFTable.XWPFBorderType.SINGLE, 4, 0, "000000");
    }

    private static void WriteImage(XWPFDocument doc, PdfImageBlock imageBlock)
    {
        var paragraph = doc.CreateParagraph();
        var run = paragraph.CreateRun();
        // OOXML 长度单位 EMU：1 磅 = 12700 EMU
        var widthEmu = (int)Math.Round(imageBlock.DisplayWidthPt * 12700);
        var heightEmu = (int)Math.Round(imageBlock.DisplayHeightPt * 12700);
        var pictureType = imageBlock.Format == PdfImageFormat.Png ? PictureType.PNG : PictureType.JPEG;
        var extension = imageBlock.Format == PdfImageFormat.Png ? "png" : "jpg";
        using var stream = new MemoryStream(imageBlock.Bytes);
        run.AddPicture(stream, (int)pictureType, $"image-{Guid.NewGuid():N}.{extension}", widthEmu, heightEmu);
    }

    private static string SanitizeXmlText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == 0x09 || c == 0x0A || c == 0x0D ||
                (c >= 0x20 && c <= 0xD7FF) ||
                (c >= 0xE000 && c <= 0xFFFD))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }

    private static IReadOnlyList<string> SplitToSingleFile(
        PdfDocument source,
        IReadOnlyList<int> pages,
        string outputFilePath,
        CancellationToken cancellationToken)
    {
        EnsureOutputDirectory(outputFilePath);
        using var target = new PdfDocument();
        foreach (var pageNumber in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            target.AddPage(source.Pages[pageNumber - 1]);
        }

        target.Save(outputFilePath);
        return [outputFilePath];
    }

    private static IReadOnlyList<string> SplitIndividualPages(
        PdfDocument source,
        IReadOnlyList<int> pages,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputFiles = new List<string>(pages.Count);
        var sourceName = Path.GetFileNameWithoutExtension(source.FullPath ?? "PDF");
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            sourceName = "PDF";
        }

        foreach (var pageNumber in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var target = new PdfDocument();
            target.AddPage(source.Pages[pageNumber - 1]);
            var outputFile = Path.Combine(outputDirectory, $"{sourceName}_第{pageNumber}页.pdf");
            target.Save(outputFile);
            outputFiles.Add(outputFile);
        }

        return outputFiles;
    }

    private static string? ValidateSourcePdf(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "请选择 PDF 文件";
        }

        if (!File.Exists(filePath))
        {
            return "PDF 文件不存在";
        }

        if (!string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return "请选择 PDF 文件";
        }

        return null;
    }

    private static string? ValidateSplitRequest(PdfSplitRequest request)
    {
        var sourceError = ValidateSourcePdf(request.SourceFilePath);
        if (sourceError is not null)
        {
            return sourceError;
        }

        if (request.Pages.Count == 0)
        {
            return "请选择需要分割的页码";
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return request.Mode == PdfSplitMode.IndividualPages
                ? "请选择分割输出目录"
                : "请选择分割输出文件";
        }

        return null;
    }

    private static string? ValidateWordExportRequest(PdfWordExportRequest request)
    {
        var sourceError = ValidateSourcePdf(request.SourceFilePath);
        if (sourceError is not null)
        {
            return sourceError;
        }

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return "请选择 Word 输出文件";
        }

        if (!string.Equals(Path.GetExtension(request.OutputFilePath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return "Word 输出文件扩展名必须为 .docx";
        }

        return null;
    }

    private static void ValidatePagesWithinDocument(IReadOnlyList<int> pages, int pageCount)
    {
        if (pages.Any(page => page < 1 || page > pageCount))
        {
            throw new InvalidOperationException("页码超出当前 PDF 页数");
        }
    }

    private static void EnsureOutputDirectory(string outputFilePath)
    {
        var directory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string BuildFriendlyError(string prefix, Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return $"{prefix}：没有访问文件或目录的权限";
        }

        if (exception is IOException)
        {
            return $"{prefix}：文件读写失败，请确认文件未被占用且路径有效";
        }

        if (exception is InvalidOperationException)
        {
            return $"{prefix}：{exception.Message}";
        }

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return $"{prefix}：处理失败，可能是 PDF 格式不兼容或文件损坏";
        }

        return $"{prefix}：{message}";
    }
}
