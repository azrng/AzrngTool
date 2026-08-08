using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;
using Ursa.Controls;

namespace AzrngTools.Views.Database.Workbench;

public partial class ConnectionDialog : UserControl
{
    private WindowToastManager? _previousManager;

    public ConnectionDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is TopLevel topLevel)
        {
            _previousManager = ToastService.SetManager(new WindowToastManager(topLevel));
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

    private async void OnBrowseSqliteFileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Sqlite database file",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Sqlite Database")
                {
                    Patterns = new[] { "*.db", "*.Sqlite", "*.sqlite3", "*.db3" }
                },
                new FilePickerFileType("All files")
                {
                    Patterns = new[] { "*", "*.*" }
                }
            }
        });

        if (files.Count == 0) return;

        if (DataContext is not ConnectionDialogViewModel vm)
        {
            return;
        }

        var localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            ToastService.ShowWarning("无法获取所选文件的本地路径。", 3000);
            return;
        }

        vm.ConnectionConfig.Database = localPath;
        ToastService.ShowInfo("Sqlite database file selected.", 2000);
    }
}
