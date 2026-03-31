namespace AzrngTools.Models;

public sealed class AppUpdateInfo
{
    public required string CurrentVersion { get; init; }

    public required string LatestVersion { get; init; }

    public required bool HasUpdate { get; init; }

    public required string DownloadUrl { get; init; }

    public required string ReleasePageUrl { get; init; }

    public required string AssetName { get; init; }

    public required string ReleaseNotes { get; init; }

    public DateTimeOffset PublishedAt { get; init; }
}
