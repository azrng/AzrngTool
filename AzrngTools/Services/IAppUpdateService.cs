using AzrngTools.Models;

namespace AzrngTools.Services;

public interface IAppUpdateService
{
    Task<AppUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    Task<AppUpdatePreparedPackage> DownloadUpdatePackageAsync(AppUpdateInfo updateInfo, CancellationToken cancellationToken = default);

    Task<AppUpdateApplyResult> ApplyPreparedUpdateAsync(AppUpdatePreparedPackage preparedPackage,
                                                        CancellationToken cancellationToken = default);
}
