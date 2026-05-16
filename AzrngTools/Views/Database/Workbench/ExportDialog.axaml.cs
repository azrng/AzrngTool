using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;
using Ursa.Controls;

namespace AzrngTools.Views.Database.Workbench;

public partial class ExportDialog : UserControl
{
    private WindowToastManager? _previousManager;

    public ExportDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _previousManager = ToastService.SetManager(new WindowToastManager(TopLevel.GetTopLevel(this)!));
        if (DataContext is ExportDialogViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;

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

    private async void OnBrowseOutputDirectoryClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择导出目录",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;

        var localPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            ToastService.ShowWarning("当前只支持导出到本地文件夹。", 3000);
            return;
        }

        var vm = (ExportDialogViewModel)DataContext!;
        vm.OutputDirectory = localPath;
    }
}
