namespace AzrngTools.Models.Pdf;

public sealed record PdfWordExportRequest(
    string SourceFilePath,
    string OutputFilePath);
