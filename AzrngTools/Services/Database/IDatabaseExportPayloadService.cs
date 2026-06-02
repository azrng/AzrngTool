using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.Services.Database;

public interface IDatabaseExportPayloadService
{
    Task<DatabaseExportPayloadResult> BuildPayloadAsync(
        ConnectionConfig connection,
        IReadOnlyCollection<ExportSelectedObjectDto> selectedObjects,
        Action<string>? progress = null);
}
