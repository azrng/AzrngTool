using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.Services.Database;

public class DatabaseWorkbenchNamingService : IDatabaseWorkbenchNamingService, ISingletonDependency
{
    public string BuildExportFilePath(ExportDialogResultDto exportRequest)
    {
        var safeDocumentName = SanitizeFileName(exportRequest.DocumentName);
        var extension = exportRequest.DocumentType switch
        {
            ExportDocumentType.Markdown => ".md",
            _ => ".xlsx"
        };

        var targetFilePath = Path.Combine(exportRequest.OutputDirectory, $"{safeDocumentName}{extension}");

        if (!File.Exists(targetFilePath))
        {
            return targetFilePath;
        }

        return Path.Combine(exportRequest.OutputDirectory, $"{safeDocumentName}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    }

    public string BuildCodeGenerationNamespace(string connectionName, string schemaName)
    {
        var safeConnection = SanitizeCodeIdentifier(connectionName);
        var safeSchema = SanitizeCodeIdentifier(schemaName);
        return $"SmartSQL.Generated.{safeConnection}.{safeSchema}";
    }

    public string SanitizeCodeIdentifier(string value)
    {
        var sanitized = SanitizeFileName(value)
            .Replace(' ', '_')
            .Replace('-', '_');

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "GeneratedItem";
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = $"Generated_{sanitized}";
        }

        return sanitized;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "export" : sanitized;
    }
}
