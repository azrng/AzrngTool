using System.Text.Json;
using AzrngTools.Models.Network;

namespace AzrngTools.Services.Network;

public sealed class ApiRequestStoreService : IApiRequestStoreService, ISingletonDependency
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private const int MaxHistoryCount = 50;
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

    public async Task AddHistoryAsync(ApiRequestSnapshot request, ApiResponseSnapshot? response, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var store = await LoadStoreCoreAsync(cancellationToken);
            store.HistoryItems.Insert(0, new ApiRequestHistoryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                Request = request,
                Response = response
            });

            if (store.HistoryItems.Count > MaxHistoryCount)
            {
                store.HistoryItems = store.HistoryItems.Take(MaxHistoryCount).ToList();
            }

            await SaveStoreCoreAsync(store, cancellationToken);
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
            return JsonSerializer.Deserialize<ApiRequestToolStore>(json, JsonOptions) ?? new ApiRequestToolStore();
        }
        catch
        {
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
        var json = JsonSerializer.Serialize(store, JsonOptions);
        var tempFilePath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);
        File.Move(tempFilePath, _filePath, true);
    }

    private static string GetStoreFilePath()
    {
        var userDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AzrngTools");
        return Path.Combine(userDataDirectory, "api-request-tool.json");
    }
}
