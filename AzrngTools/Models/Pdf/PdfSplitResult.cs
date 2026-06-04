namespace AzrngTools.Models.Pdf;

public sealed record PdfSplitResult(
    IReadOnlyList<string> OutputFiles,
    bool IsEvaluationMode)
{
    public int OutputFileCount => OutputFiles.Count;
}
