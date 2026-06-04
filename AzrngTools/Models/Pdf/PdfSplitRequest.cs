namespace AzrngTools.Models.Pdf;

public sealed record PdfSplitRequest(
    string SourceFilePath,
    IReadOnlyList<int> Pages,
    PdfSplitMode Mode,
    string OutputPath);
