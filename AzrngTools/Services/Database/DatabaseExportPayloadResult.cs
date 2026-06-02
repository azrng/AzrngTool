using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public class DatabaseExportPayloadResult
{
    public bool Success { get; init; }

    public List<TableModel> Tables { get; init; } = [];

    public List<ViewModel> Views { get; init; } = [];

    public List<StoredProcedureModel> Procedures { get; init; } = [];

    public Dictionary<string, List<ColumnModel>> TableColumnsMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<IndexModel>> TableIndexesMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string Message { get; init; } = string.Empty;

    public int TotalObjectCount => Tables.Count + Views.Count + Procedures.Count;
}
