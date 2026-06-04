namespace AzrngTools.Models.Pdf;

public sealed record PdfLoadedFileSummary(
    string FilePath,
    string FileName,
    long FileSizeBytes,
    int PageCount,
    bool IsEvaluationMode,
    string LicenseStatusText);
