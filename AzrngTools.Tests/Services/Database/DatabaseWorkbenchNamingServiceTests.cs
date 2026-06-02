using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseWorkbenchNamingServiceTests
{
    [Theory]
    [InlineData("order-detail", "order_detail")]
    [InlineData("123 users", "Generated_123_users")]
    [InlineData("", "export")]
    public void SanitizeCodeIdentifier_returns_safe_identifier(string value, string expected)
    {
        var service = new DatabaseWorkbenchNamingService();

        var result = service.SanitizeCodeIdentifier(value);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildCodeGenerationNamespace_uses_safe_connection_and_schema()
    {
        var service = new DatabaseWorkbenchNamingService();

        var result = service.BuildCodeGenerationNamespace("pg-prod", "123 public");

        Assert.Equal("SmartSQL.Generated.pg_prod.Generated_123_public", result);
    }

    [Theory]
    [InlineData(ExportDocumentType.Excel, ".xlsx")]
    [InlineData(ExportDocumentType.Markdown, ".md")]
    public void BuildExportFilePath_uses_document_type_extension(ExportDocumentType documentType, string extension)
    {
        var service = new DatabaseWorkbenchNamingService();
        var request = new ExportDialogResultDto
        {
            DocumentName = "db doc",
            OutputDirectory = Path.GetTempPath(),
            DocumentType = documentType
        };

        var result = service.BuildExportFilePath(request);

        Assert.EndsWith($"db doc{extension}", result);
    }

    [Fact]
    public void BuildExportFilePath_appends_timestamp_when_file_exists()
    {
        var service = new DatabaseWorkbenchNamingService();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"azrng-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var existingFile = Path.Combine(outputDirectory, "db.xlsx");
        File.WriteAllText(existingFile, string.Empty);
        var request = new ExportDialogResultDto
        {
            DocumentName = "db",
            OutputDirectory = outputDirectory,
            DocumentType = ExportDocumentType.Excel
        };

        try
        {
            var result = service.BuildExportFilePath(request);

            Assert.NotEqual(existingFile, result);
            Assert.StartsWith(Path.Combine(outputDirectory, "db_"), result);
            Assert.EndsWith(".xlsx", result);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }
}
