using AzrngTools.Models.Network;

namespace AzrngTools.Services.Network;

public interface IApiRequestExecutionService
{
    Task<ApiRequestExecutionResult> SendAsync(
        ApiRequestSnapshot request,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken);
}
