using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface ICodeGenerationService
{
    Task<bool> GenerateEntityClassAsync(
        string outputPath,
        string className,
        List<ColumnModel> columns,
        string namespaceName = "SmartSQL.Entities");

    Task<bool> GenerateEntityClassesAsync(
        string outputPath,
        List<TableModel> tables,
        Dictionary<string, List<ColumnModel>> tableColumnsMap,
        string namespaceName = "SmartSQL.Entities");

    Task<bool> GenerateRepositoryAsync(
        string outputPath,
        string entityName,
        string namespaceName = "SmartSQL.Repositories");

    Task<bool> GenerateControllerAsync(
        string outputPath,
        string entityName,
        string namespaceName = "SmartSQL.Controllers");
}
