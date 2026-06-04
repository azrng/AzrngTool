namespace AzrngTools.Models.Pdf;

public sealed record PdfPageSelectionResult(
    bool IsSuccess,
    IReadOnlyList<int> Pages,
    string? ErrorMessage)
{
    public int Count => Pages.Count;

    public static PdfPageSelectionResult Success(IReadOnlyList<int> pages)
    {
        return new PdfPageSelectionResult(true, pages, null);
    }

    public static PdfPageSelectionResult Failure(string errorMessage)
    {
        return new PdfPageSelectionResult(false, Array.Empty<int>(), errorMessage);
    }
}
