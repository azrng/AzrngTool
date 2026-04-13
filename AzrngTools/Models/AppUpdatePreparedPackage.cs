namespace AzrngTools.Models;

public sealed class AppUpdatePreparedPackage
{
    public required string Version { get; init; }

    public required string AssetName { get; init; }

    public required string PackageRoot { get; init; }

    public required string PayloadDirectory { get; init; }

    public DateTimeOffset DownloadedAt { get; init; }
}
