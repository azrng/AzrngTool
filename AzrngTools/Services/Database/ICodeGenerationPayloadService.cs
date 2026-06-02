using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface ICodeGenerationPayloadService
{
    Task<CodeGenerationPayloadResult> BuildPayloadAsync(
        ConnectionConfig connection,
        string schemaName,
        Action<string>? progress = null);
}
