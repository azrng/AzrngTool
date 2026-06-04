namespace AzrngTools.Models.Pdf;

public sealed record PdfWordExportResult(
    string OutputFilePath,
    bool IsEvaluationMode);
