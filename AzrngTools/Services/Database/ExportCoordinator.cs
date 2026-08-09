using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.ViewModels.Database;
using AzrngTools.Views.Database.Workbench;

namespace AzrngTools.Services.Database;

public class ExportCoordinator : IExportCoordinator
{
    private readonly IDatabaseService _databaseService;
    private readonly IDocumentExportService _documentExportService;
    private readonly ICodeGenerationService _codeGenerationService;
    private readonly IDatabaseExportPayloadService _databaseExportPayloadService;
    private readonly ICodeGenerationPayloadService _codeGenerationPayloadService;
    private readonly IDatabaseWorkbenchNamingService _databaseWorkbenchNamingService;
    private string? _lastDocumentExportDirectory;

    public ExportCoordinator(
        IDatabaseService databaseService,
        IDocumentExportService? documentExportService = null,
        ICodeGenerationService? codeGenerationService = null,
        IDatabaseExportPayloadService? databaseExportPayloadService = null,
        ICodeGenerationPayloadService? codeGenerationPayloadService = null,
        IDatabaseWorkbenchNamingService? databaseWorkbenchNamingService = null)
    {
        _databaseService = databaseService;
        _documentExportService = documentExportService ?? new DocumentExportService();
        _codeGenerationService = codeGenerationService ?? new CodeGenerationService();
        _databaseExportPayloadService = databaseExportPayloadService ?? new DatabaseExportPayloadService(databaseService);
        _codeGenerationPayloadService = codeGenerationPayloadService ?? new CodeGenerationPayloadService(databaseService);
        _databaseWorkbenchNamingService = databaseWorkbenchNamingService ?? new DatabaseWorkbenchNamingService();
    }

    public async Task OpenExportDialogAsync(
        Window ownerWindow,
        ConnectionConfig connection,
        string? databaseName,
        string? schemaName,
        Action<bool>? setLoading = null,
        Action<string?>? setLoadingText = null)
    {
        var dialogViewModel = new ExportDialogViewModel(
            connection,
            databaseName,
            schemaName,
            _lastDocumentExportDirectory,
            _databaseService);
        var exportRequest = await Ursa.Controls.Dialog.ShowCustomAsync<ExportDialog, ExportDialogViewModel, ExportDialogResultDto?>(
            dialogViewModel, ownerWindow, new Ursa.Controls.DialogOptions { Title = string.Empty, CanResize = false });
        if (exportRequest == null)
        {
            return;
        }

        _lastDocumentExportDirectory = exportRequest.OutputDirectory;

        setLoading?.Invoke(true);
        setLoadingText?.Invoke("正在准备导出数据...");

        try
        {
            var exportPayload = await _databaseExportPayloadService.BuildPayloadAsync(
                connection,
                exportRequest.SelectedObjects,
                progress => setLoadingText?.Invoke(progress));
            if (!exportPayload.Success)
            {
                setLoadingText?.Invoke(exportPayload.Message);
                ToastService.ShowError(exportPayload.Message, 5000);
                return;
            }

            if (exportPayload.TotalObjectCount == 0)
            {
                setLoadingText?.Invoke("没有可导出的数据。");
                ToastService.ShowWarning("没有可导出的数据。", 3000);
                return;
            }

            setLoadingText?.Invoke($"正在导出 {exportPayload.TotalObjectCount} 个对象...");
            var exportFilePath = _databaseWorkbenchNamingService.BuildExportFilePath(exportRequest);
            var exported = exportRequest.DocumentType switch
            {
                ExportDocumentType.Excel => await _documentExportService.ExportToExcelAsync(
                    exportFilePath,
                    exportRequest.DocumentName,
                    exportPayload.Tables,
                    exportPayload.TableColumnsMap,
                    exportPayload.Views,
                    exportPayload.Procedures,
                    exportPayload.TableIndexesMap),
                ExportDocumentType.Markdown => await _documentExportService.ExportToMarkdownAsync(
                    exportFilePath,
                    exportRequest.DocumentName,
                    exportPayload.Tables,
                    exportPayload.TableColumnsMap,
                    exportPayload.Views,
                    exportPayload.Procedures,
                    exportPayload.TableIndexesMap),
                _ => false
            };

            if (!exported)
            {
                setLoadingText?.Invoke("文档导出失败。");
                ToastService.ShowError("文档导出失败，请查看日志获取详情。", 5000);
                return;
            }

            setLoadingText?.Invoke($"导出成功，共 {exportPayload.TotalObjectCount} 个对象");
            ToastService.ShowSuccess($"导出成功\n共 {exportPayload.TotalObjectCount} 个对象", 4000);
        }
        catch (Exception ex)
        {
            setLoadingText?.Invoke("文档导出失败。");
            LoggingService.LogError("Document export failed.", ex);
            ToastService.ShowError($"文档导出失败：{ex.Message}", 6000);
        }
        finally
        {
            setLoading?.Invoke(false);
        }
    }

    public async Task GenerateCodeAsync(
        Window ownerWindow,
        ConnectionConfig connection,
        string? schemaName,
        string? mode,
        Action<bool>? setLoading = null,
        Action<string?>? setLoadingText = null)
    {
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            ToastService.ShowWarning("生成代码前请先在对象树中选择架构。", 3000);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(ownerWindow);
        if (topLevel?.StorageProvider == null)
        {
            LoggingService.LogWarning("Storage provider is unavailable for code generation.");
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择代码输出目录",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        var outputRootPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(outputRootPath))
        {
            ToastService.ShowWarning("所选输出目录不是本地文件系统路径。", 4000);
            return;
        }

        var normalizedMode = (mode ?? "all").Trim().ToLowerInvariant();
        var generateEntities = normalizedMode is "entity" or "entities" or "all";
        var generateRepositories = normalizedMode is "repository" or "repositories" or "all";
        var generateControllers = normalizedMode is "controller" or "controllers" or "all";

        if (!generateEntities && !generateRepositories && !generateControllers)
        {
            ToastService.ShowWarning($"不支持的代码生成模式：{mode}", 4000);
            return;
        }

        setLoading?.Invoke(true);
        setLoadingText?.Invoke($"正在准备为架构 {schemaName} 生成代码...");

        try
        {
            var payload = await _codeGenerationPayloadService.BuildPayloadAsync(
                connection,
                schemaName,
                progress => setLoadingText?.Invoke(progress));
            if (!payload.Success)
            {
                setLoadingText?.Invoke(payload.Message);
                ToastService.ShowError(payload.Message, 5000);
                return;
            }

            if (payload.Tables.Count == 0)
            {
                setLoadingText?.Invoke("没有可生成代码的数据表。");
                ToastService.ShowWarning("没有可生成代码的数据表。", 3000);
                return;
            }

            var namespaceRoot = _databaseWorkbenchNamingService.BuildCodeGenerationNamespace(connection.Name, schemaName);
            var generatedFileCount = 0;
            var failedItems = new List<string>();

            foreach (var table in payload.Tables.OrderBy(table => table.Name))
            {
                payload.TableColumnsMap.TryGetValue(table.Name, out var columns);
                columns ??= new List<ColumnModel>();

                if (generateEntities)
                {
                    setLoadingText?.Invoke($"正在生成实体类：{table.Name} ...");
                    var entityPath = Path.Combine(outputRootPath, "Entities", $"{_databaseWorkbenchNamingService.SanitizeCodeIdentifier(table.Name)}.cs");
                    var entitySuccess = await _codeGenerationService.GenerateEntityClassAsync(entityPath, table.Name, columns, $"{namespaceRoot}.Entities");
                    if (entitySuccess)
                    {
                        generatedFileCount++;
                    }
                    else
                    {
                        failedItems.Add($"Entity:{table.Name}");
                    }
                }

                if (generateRepositories)
                {
                    setLoadingText?.Invoke($"正在生成仓储类：{table.Name} ...");
                    var repositoryPath = Path.Combine(outputRootPath, "Repositories", $"{_databaseWorkbenchNamingService.SanitizeCodeIdentifier(table.Name)}Repository.cs");
                    var repositorySuccess = await _codeGenerationService.GenerateRepositoryAsync(repositoryPath, table.Name, $"{namespaceRoot}.Repositories");
                    if (repositorySuccess)
                    {
                        generatedFileCount++;
                    }
                    else
                    {
                        failedItems.Add($"Repository:{table.Name}");
                    }
                }

                if (generateControllers)
                {
                    setLoadingText?.Invoke($"正在生成控制器：{table.Name} ...");
                    var controllerPath = Path.Combine(outputRootPath, "Controllers", $"{_databaseWorkbenchNamingService.SanitizeCodeIdentifier(table.Name)}Controller.cs");
                    var controllerSuccess = await _codeGenerationService.GenerateControllerAsync(controllerPath, table.Name, $"{namespaceRoot}.Controllers");
                    if (controllerSuccess)
                    {
                        generatedFileCount++;
                    }
                    else
                    {
                        failedItems.Add($"Controller:{table.Name}");
                    }
                }
            }

            if (failedItems.Count > 0)
            {
                setLoadingText?.Invoke($"已生成 {generatedFileCount} 个文件，失败 {failedItems.Count} 项。");
                ToastService.ShowWarning($"已生成 {generatedFileCount} 个文件，但有 {failedItems.Count} 项失败，请查看日志。", 6000);
                LoggingService.LogWarning($"Code generation partial failure: {string.Join(", ", failedItems)}");
                return;
            }

            setLoadingText?.Invoke($"已生成 {generatedFileCount} 个文件到 {outputRootPath}。");
            ToastService.ShowSuccess($"已为架构 {schemaName} 生成 {generatedFileCount} 个文件。", 4000);
            LoggingService.LogOperation($"Generated {generatedFileCount} code files to {outputRootPath}.");
        }
        catch (Exception ex)
        {
            setLoadingText?.Invoke("代码生成失败。");
            LoggingService.LogError("代码生成失败。", ex);
            ToastService.ShowError($"代码生成失败：{ex.Message}", 6000);
        }
        finally
        {
            setLoading?.Invoke(false);
        }
    }
}
