using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IDocumentExportService
{
    Task<bool> ExportToExcelAsync(
        string filePath,
        string databaseName,
        List<TableModel> tables,
        Dictionary<string, List<ColumnModel>> tableColumnsMap,
        List<ViewModel>? views = null,
        List<StoredProcedureModel>? procedures = null,
        Dictionary<string, List<IndexModel>>? tableIndexesMap = null);

    Task<bool> ExportToMarkdownAsync(
        string filePath,
        string databaseName,
        List<TableModel> tables,
        Dictionary<string, List<ColumnModel>> tableColumnsMap,
        List<ViewModel>? views = null,
        List<StoredProcedureModel>? procedures = null,
        Dictionary<string, List<IndexModel>>? tableIndexesMap = null);
}
