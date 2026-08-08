using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

/// <summary>
/// pg_dump 导出向导编排器
/// </summary>
public interface IPgDumpExportCoordinator
{
    /// <summary>
    /// 打开 pg_dump 导出向导并在确认后执行导出
    /// </summary>
    Task OpenPgDumpDialogAsync(
        Window ownerWindow,
        ConnectionConfig connection,
        string? databaseName,
        Action<bool>? setLoading = null,
        Action<string?>? setLoadingText = null);
}
