using AzrngTools.Models.Network;

namespace AzrngTools.Services.Network;

public interface IApiRequestStoreService
{
    Task<IReadOnlyList<ApiRequestHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken);

    Task AddHistoryAsync(ApiRequestSnapshot request, ApiResponseSnapshot? response, CancellationToken cancellationToken);

    Task ClearHistoryAsync(CancellationToken cancellationToken);

    Task DeleteHistoryAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiEnvironmentVariable>> GetEnvironmentVariablesAsync(CancellationToken cancellationToken);

    Task SaveEnvironmentVariablesAsync(
        string activeEnvironmentName,
        IReadOnlyList<ApiEnvironmentVariable> variables,
        CancellationToken cancellationToken);

    Task<string> GetActiveEnvironmentNameAsync(CancellationToken cancellationToken);
}
