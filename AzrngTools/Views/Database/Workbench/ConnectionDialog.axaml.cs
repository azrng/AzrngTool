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
        _previousManager = ToastService.SetManager(new WindowToastManager(TopLevel.GetTopLevel(this)!));
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

        var vm = (ConnectionDialogViewModel)DataContext!;
        vm.ConnectionConfig.Database = files[0].Path.LocalPath;
        ToastService.ShowInfo("Sqlite database file selected.", 2000);
    }
}
