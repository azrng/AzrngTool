using System.Text.Json;
using System.Text.Json.Serialization;
using AzrngTools.Models.Network;

namespace AzrngTools.Services.Network;

public sealed partial class ApiRequestStoreService : IApiRequestStoreService, ISingletonDependency
{
    private const int MaxHistoryCount = 50;

    // 历史入库的请求/响应体截断上限：不截断时 50 条大响应会把历史文件撑到几十 MB，每次请求都全量读写
    private const int MaxStoredBodyLength = 64 * 1024;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath;

    public ApiRequestStoreService()
    {
        _filePath = GetStoreFilePath();
    }

    public async Task<IReadOnlyList<ApiRequestHistoryItem>> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var store = await LoadStoreAsync(cancellationToken);
        return store.HistoryItems
            .OrderByDescending(item => item.Timestamp)
            .ToList();
    }

    public async Task<IReadOnlyList<ApiRequestHistoryItem>> AddHistoryAsync(ApiRequestSnapshot request, ApiResponseSnapshot? response, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken);
            store.HistoryItems.Insert(0, new ApiRequestHistoryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                Request = TruncateRequest(request),
                Response = TruncateResponse(response)
            });

            if (store.HistoryItems.Count > MaxHistoryCount)
            {
                store.HistoryItems = store.HistoryItems.Take(MaxHistoryCount).ToList();
            }

            await SaveStoreCoreAsync(store, cancellationToken);
            return store.HistoryItems
                .OrderByDescending(item => item.Timestamp)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken);
            store.HistoryItems.Clear();
            await SaveStoreCoreAsync(store, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task DeleteHistoryAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken);
            store.HistoryItems.RemoveAll(item => item.Id == id);
            await SaveStoreCoreAsync(store, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<ApiRequestToolStore> LoadStoreAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return await LoadStoreCoreAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<ApiRequestToolStore> LoadStoreCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ApiRequestToolStore();
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            // 反序列化 50 条历史是 CPU 重活，移到线程池执行避免占用调用方（UI）线程
            return await Task.Run(() =>
                JsonSerializer.Deserialize(json, ApiRequestJsonContext.Default.ApiRequestToolStore) ?? new ApiRequestToolStore(), cancellationToken);
        }
        catch (Exception ex)
        {
            LocalLogHelper.LogError($"加载接口调试历史记录失败: {ex.Message}\n{ex.GetExceptionAndStack()}");
            return new ApiRequestToolStore();
        }
    }

    private async Task SaveStoreCoreAsync(ApiRequestToolStore store, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        // 序列化 50 条历史同样是 CPU 重活，与读取侧一致移到线程池
        var json = await Task.Run(() => JsonSerializer.Serialize(store, ApiRequestJsonContext.Default.ApiRequestToolStore), cancellationToken);
        var tempFilePath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
        File.Move(tempFilePath, _filePath, true);
    }

    /// <summary>
    /// 把超大的请求体截断到入库上限；快照由每次请求新建，就地截断不影响其他持有者。
    /// </summary>
    private static ApiRequestSnapshot TruncateRequest(ApiRequestSnapshot request)
    {
        request.BodyContent = TruncateBody(request.BodyContent);
        return request;
    }

    /// <summary>
    /// 把超大响应体截断到入库上限；状态与头部等元数据原样保留。
    /// </summary>
    private static ApiResponseSnapshot? TruncateResponse(ApiResponseSnapshot? response)
    {
        if (response is null)
        {
            return null;
        }

        response.Content = TruncateBody(response.Content);
        return response;
    }

    private static string TruncateBody(string body)
    {
        if (body.Length <= MaxStoredBodyLength)
        {
            return body;
        }

        return string.Concat(body.AsSpan(0, MaxStoredBodyLength), $"\n\n--- 内容过大（原始 {body.Length} 字符），入库时已截断 ---");
    }

    private static string GetStoreFilePath()
    {
        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools");
        return Path.Combine(userDataDirectory, "api-request-tool.json");
    }

    // 源生成 JSON 上下文：AOT 发布下不能用反射序列化
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(ApiRequestToolStore))]
    private sealed partial class ApiRequestJsonContext : JsonSerializerContext
    {
    }
}
