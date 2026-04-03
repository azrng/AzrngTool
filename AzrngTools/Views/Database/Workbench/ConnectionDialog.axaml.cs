using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SmartSQL.UI.Models;
using SmartSQL.UI.Services;
using SmartSQL.UI.ViewModels;

namespace SmartSQL.UI.Views;

public partial class ConnectionDialog : Window
{
    private Panel? _previousToastContainer;

    public ConnectionDialogViewModel ViewModel => (ConnectionDialogViewModel)DataContext!;

    public ConnectionDialog() : this(null, null, null)
    {
    }

    public ConnectionDialog(
        ObservableCollection<ConnectionConfig>? connections = null,
        Action? persistConnections = null,
        ConnectionConfig? selectedConnection = null)
    {
        InitializeComponent();
        DataContext = new ConnectionDialogViewModel(connections, persistConnections, selectedConnection);
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        var toastContainer = this.FindControl<StackPanel>("ToastContainer");
        if (toastContainer != null)
        {
            _previousToastContainer = ToastService.SetContainer(toastContainer);
        }

        ViewModel.CloseRequested += OnCloseRequested;
        Closed += OnDialogClosed;
    }

    private void OnCloseRequested(object? sender, bool dialogResult)
    {
        Close(dialogResult ? ViewModel.DialogResultConnection : null);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        Closed -= OnDialogClosed;

        if (_previousToastContainer != null)
        {
            ToastService.SetContainer(_previousToastContainer);
            _previousToastContainer = null;
        }
        else
        {
            ToastService.ClearContainer();
        }
    }

    private async void OnBrowseSqliteFileClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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

        if (files.Count == 0)
        {
            return;
        }

        ViewModel.ConnectionConfig.Database = files[0].Path.LocalPath;
        ToastService.ShowInfo("Sqlite database file selected.", 2000);
    }
}
