using AzrngTools.Models.Network;

namespace AzrngTools.Services.Network;

public interface IApiRequestStoreService
{
    Task<IReadOnlyList<ApiRequestHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 写入一条历史并返回裁剪后的最新历史列表（按时间倒序），调用方无需再全量回读。
    /// </summary>
    Task<IReadOnlyList<ApiRequestHistoryItem>> AddHistoryAsync(ApiRequestSnapshot request, ApiResponseSnapshot? response, CancellationToken cancellationToken);

    Task ClearHistoryAsync(CancellationToken cancellationToken);

    Task DeleteHistoryAsync(string id, CancellationToken cancellationToken);
}
