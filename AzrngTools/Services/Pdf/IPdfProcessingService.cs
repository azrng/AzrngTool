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
}
