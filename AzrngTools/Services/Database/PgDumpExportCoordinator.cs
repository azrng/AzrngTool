using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.ViewModels.Database;
using AzrngTools.Views.Database.Workbench;

namespace AzrngTools.Services.Database;

/// <summary>
/// pg_dump 导出向导编排器：打开向导 → 收集结果 → 调用 IPgDumpService → Toast 反馈。
/// 仿 ExportCoordinator.OpenExportDialogAsync。
/// </summary>
public class PgDumpExportCoordinator : IPgDumpExportCoordinator
{
    private readonly IDatabaseService _databaseService;
    private readonly IPgDumpService _pgDumpService;
    private string? _lastPgDumpExePath;

    public PgDumpExportCoordinator(IDatabaseService databaseService, IPgDumpService? pgDumpService = null)
    {
        _databaseService = databaseService;
        _pgDumpService = pgDumpService ?? new PgDumpService();
    }

    public async Task OpenPgDumpDialogAsync(
        Window ownerWindow,
        ConnectionConfig connection,
        string? databaseName,
        Action<bool>? setLoading = null,
        Action<string?>? setLoadingText = null)
    {
        if (connection.DatabaseType != DatabaseType.PostgresSql)
        {
            ToastService.ShowWarning("pg_dump 导出仅支持 PostgreSQL 连接。", 3000);
            return;
        }

        var dialogViewModel = new PgDumpExportDialogViewModel(
            connection, databaseName, _databaseService, _lastPgDumpExePath);

        var result = await Ursa.Controls.Dialog.ShowCustomAsync
            <PgDumpExportDialog, PgDumpExportDialogViewModel, PgDumpExportResultDto?>(
                dialogViewModel, ownerWindow,
                new Ursa.Controls.DialogOptions { Title = "pg_dump 导出", CanResize = false });

        if (result == null)
        {
            return;
        }

        _lastPgDumpExePath = result.PgDumpExePath;

        setLoading?.Invoke(true);
        setLoadingText?.Invoke("正在执行 pg_dump 导出...");
        try
        {
            var connForDump = CloneConnectionWithDatabase(connection, result.Database);
            var request = new PgDumpRequest
            {
                Connection = connForDump,
                Database = result.Database,
                Schemas = result.SelectedSchemas,
                Mode = result.ContentMode,
                OutputFormat = result.OutputFormat,
                PgDumpExePath = result.PgDumpExePath,
                OutputFilePath = result.OutputFilePath
            };

            var progress = new Progress<string>(p => setLoadingText?.Invoke(p));
            var dumpResult = await _pgDumpService.ExecuteDumpAsync(request, progress);
            if (dumpResult.Success)
            {
                setLoadingText?.Invoke("导出完成");
                ToastService.ShowSuccess($"导出成功\n{dumpResult.OutputPath}", 5000);
                LoggingService.LogOperation($"pg_dump export succeeded: {dumpResult.OutputPath}");
            }
            else
            {
                var message = string.IsNullOrWhiteSpace(dumpResult.StdError)
                    ? $"pg_dump 退出码 {dumpResult.ExitCode}"
                    : dumpResult.StdError;
                if (message.Contains("server version mismatch", StringComparison.OrdinalIgnoreCase))
                {
                    message = "pg_dump 与服务器版本不匹配，请使用与 PostgreSQL 服务器同主版本（或更高）的 pg_dump。\n" + message;
                }

                ToastService.ShowError($"pg_dump 导出失败：{message}", 8000);
            }
        }
        catch (Exception ex)
        {
            LoggingService.LogError("pg_dump export failed.", ex);
            ToastService.ShowError($"pg_dump 导出失败：{ex.Message}", 6000);
        }
        finally
        {
            setLoading?.Invoke(false);
        }
    }

    private static ConnectionConfig CloneConnectionWithDatabase(ConnectionConfig source, string database)
    {
        return new ConnectionConfig
        {
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            Database = database
        };
    }
}
