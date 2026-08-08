using AzrngTools.Models.Database.DTOs;

namespace AzrngTools.Services.Database;

/// <summary>
/// pg_dump 进程封装服务
/// </summary>
public interface IPgDumpService
{
    /// <summary>
    /// 执行一次 pg_dump 导出
    /// </summary>
    Task<PgDumpResult> ExecuteDumpAsync(
        PgDumpRequest request,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
