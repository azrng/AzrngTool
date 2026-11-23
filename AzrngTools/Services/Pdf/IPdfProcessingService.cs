using Azrng.Core.Results;
using AzrngTools.Models.Pdf;

namespace AzrngTools.Services.Pdf;

public interface IPdfProcessingService
{
    string LicenseStatusText { get; }

    bool IsEvaluationMode { get; }

    Task<IResultModel<PdfLoadedFileSummary>> LoadMetadataAsync(string filePath, CancellationToken cancellationToken = default);

    Task<IResultModel<PdfSplitResult>> SplitAsync(PdfSplitRequest request, CancellationToken cancellationToken = default);

    Task<IResultModel<PdfWordExportResult>> ExportWordAsync(PdfWordExportRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把 PDF 转换为 Markdown 并写出：标题按书签/结构标签/字号三级判定，图片按请求开关导出到 md 同级 .assets 目录或整体忽略；图片型（扫描件）内容拒绝转换。
    /// </summary>
    Task<IResultModel<PdfMarkdownExportResult>> ExportMarkdownAsync(PdfMarkdownExportRequest request, CancellationToken cancellationToken = default);
}
