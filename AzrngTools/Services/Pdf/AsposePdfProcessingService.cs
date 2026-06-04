using Azrng.Core.Helpers;
using Azrng.Core.Results;
using AzrngTools.Models.Pdf;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace AzrngTools.Services.Pdf;

public sealed class AsposePdfProcessingService : IPdfProcessingService, ITransientDependency
{
    public string LicenseStatusText => "使用开源组件（PdfPig + PDFsharp + OpenXML），无需授权";

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
                LocalLogHelper.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
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
                LocalLogHelper.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
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
                LocalLogHelper.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return PdfResultModel.Failure<PdfWordExportResult>(friendlyMessage);
            }
        }, cancellationToken);
    }

    private static void ExportPdfToWord(string pdfPath, string outputPath, CancellationToken cancellationToken)
    {
        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.Body = new Body();

        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfPath))
        {
            var totalPages = pdf.NumberOfPages;

            for (int pageNum = 1; pageNum <= totalPages; pageNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = pdf.GetPage(pageNum);
                var words = page.GetWords().ToList();

                if (words.Count == 0)
                    continue;

                var lines = GroupWordsIntoLines(words);

                if (pageNum > 1)
                {
                    body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }

                foreach (var line in lines)
                {
                    var lineText = SanitizeXmlText(string.Join(" ", line.Select(w => w.Text)));
                    if (string.IsNullOrWhiteSpace(lineText))
                        continue;

                    var run = new Run(new Text(lineText) { Space = SpaceProcessingModeValues.Preserve });

                    if (line.Count > 0)
                    {
                        var letters = line[0].Letters;
                        if (letters.Count > 0)
                        {
                            var fontSize = letters[0].FontSize;
                            if (fontSize > 0)
                            {
                                run.RunProperties = new RunProperties(
                                    new FontSize { Val = ((int)(fontSize * 2)).ToString() });
                            }
                        }
                    }

                    body.Append(new Paragraph(run));
                }
            }
        }
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

    private static List<List<UglyToad.PdfPig.Content.Word>> GroupWordsIntoLines(List<UglyToad.PdfPig.Content.Word> words)
    {
        var lines = new List<List<UglyToad.PdfPig.Content.Word>>();

        if (words.Count == 0)
            return lines;

        var sorted = words
            .OrderByDescending(w => w.BoundingBox.Bottom)
            .ThenBy(w => w.BoundingBox.Left)
            .ToList();

        var currentLine = new List<UglyToad.PdfPig.Content.Word> { sorted[0] };
        var currentY = sorted[0].BoundingBox.Bottom;

        for (int i = 1; i < sorted.Count; i++)
        {
            var word = sorted[i];
            var yDiff = Math.Abs(word.BoundingBox.Bottom - currentY);

            if (yDiff > 3.0)
            {
                currentLine.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
                lines.Add(currentLine);
                currentLine = new List<UglyToad.PdfPig.Content.Word>();
                currentY = word.BoundingBox.Bottom;
            }

            currentLine.Add(word);
        }

        if (currentLine.Count > 0)
        {
            currentLine.Sort((a, b) => a.BoundingBox.Left.CompareTo(b.BoundingBox.Left));
            lines.Add(currentLine);
        }

        return lines;
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
