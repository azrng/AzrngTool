using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;
using Ursa.Controls;

namespace AzrngTools.Views.Database.Workbench;

public partial class PgDumpExportDialog : UserControl
{
    private WindowToastManager? _previousManager;

    public PgDumpExportDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
        {
            _previousManager = ToastService.SetManager(new WindowToastManager(topLevel));
        }
        if (DataContext is PgDumpExportDialogViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_previousManager != null)
        {
            ToastService.SetManager(_previousManager);
            _previousManager = null;
        }
        else
        {
            ToastService.ClearManager();
        }
    }

    private async void OnBrowsePgDumpExeClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 pg_dump 可执行文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("可执行文件") { Patterns = new[] { "*.exe" } },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*", "*.*" } }
            }
        });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            ToastService.ShowWarning("无法获取所选文件的本地路径。", 3000);
            return;
        }

        if (DataContext is PgDumpExportDialogViewModel vm)
        {
            vm.PgDumpExePath = path;
        }
    }

    private async void OnBrowseOutputFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null || DataContext is not PgDumpExportDialogViewModel vm)
        {
            return;
        }

        var isCustom = vm.OutputFormat == PgDumpOutputFormat.Custom;
        var extension = isCustom ? "dump" : "sql";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择导出文件保存位置",
            DefaultExtension = extension,
            SuggestedFileName = vm.SuggestedOutputFileName,
            FileTypeChoices = new[]
            {
                new FilePickerFileType(isCustom ? "pg_dump 归档 (*.dump)" : "SQL 脚本 (*.sql)")
                {
                    Patterns = new[] { isCustom ? "*.dump" : "*.sql" }
                },
                new FilePickerFileType("所有文件") { Patterns = new[] { "*", "*.*" } }
            }
        });

        if (file == null)
        {
            return;
        }

        var path = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.OutputFilePath = path;
        }
    }
}
