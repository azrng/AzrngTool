using AzrngTools.Models;

namespace AzrngTools.Services;

public interface IAppUpdateService
{
    Task<AppUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateApplyResult> DownloadAndPrepareUpdateAsync(AppUpdateInfo updateInfo, CancellationToken cancellationToken = default);
}
