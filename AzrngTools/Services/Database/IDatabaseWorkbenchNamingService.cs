using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.Services.Database;

public interface IDatabaseWorkbenchNamingService
{
    string BuildExportFilePath(ExportDialogResultDto exportRequest);

    string BuildCodeGenerationNamespace(string connectionName, string schemaName);

    string SanitizeCodeIdentifier(string value);
}
