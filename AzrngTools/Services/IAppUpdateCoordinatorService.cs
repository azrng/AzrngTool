namespace AzrngTools.Services;

public interface IAppUpdateCoordinatorService
{
    string LatestVersion { get; }

    string ReleasePublishedAt { get; }

    string UpdateStatus { get; }

    string PrimaryActionText { get; }

    bool IsBusy { get; }

    bool CanExecutePrimaryAction { get; }

    Task EnsureStartupCheckAsync(CancellationToken cancellationToken = default);

    Task ExecutePrimaryActionAsync(CancellationToken cancellationToken = default);
}
