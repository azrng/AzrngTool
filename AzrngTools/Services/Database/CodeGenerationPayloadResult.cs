using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public class CodeGenerationPayloadResult
{
    public bool Success { get; init; }

    public List<TableModel> Tables { get; init; } = [];

    public Dictionary<string, List<ColumnModel>> TableColumnsMap { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string Message { get; init; } = string.Empty;
}
