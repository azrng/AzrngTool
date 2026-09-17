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

    /// <summary>
    /// 应用退出时尝试静默应用已就绪的更新包（只替换文件、不重启，下次启动即为新版本）；无可用更新包或本轮已发起过应用时静默跳过。
    /// </summary>
    void TryApplyPreparedUpdateOnExit();
}
