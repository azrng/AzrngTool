using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IExportCoordinator
{
    Task OpenExportDialogAsync(
        Window ownerWindow,
        ConnectionConfig connection,
        string? databaseName,
        string? schemaName,
        Action<bool>? setLoading = null,
        Action<string?>? setLoadingText = null);

    Task GenerateCodeAsync(
        Window ownerWindow,
        ConnectionConfig connection,
        string? schemaName,
        string? mode,
        Action<bool>? setLoading = null,
        Action<string?>? setLoadingText = null);
}
