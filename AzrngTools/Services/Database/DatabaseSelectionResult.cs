namespace AzrngTools.Services.Database;

public sealed class DatabaseSelectionResult
{
    public DatabaseSelectionResult(IReadOnlyList<string> databases, string? selectedDatabase)
    {
        Databases = databases;
        SelectedDatabase = selectedDatabase;
    }

    public IReadOnlyList<string> Databases { get; }

    public string? SelectedDatabase { get; }
}
