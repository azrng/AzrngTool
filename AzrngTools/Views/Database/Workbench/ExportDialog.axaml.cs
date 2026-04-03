using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SmartSQL.UI.Services;
using SmartSQL.UI.ViewModels;

namespace SmartSQL.UI.Views;

public partial class ExportDialog : Window
{
    private Panel? _previousToastContainer;

    public ExportDialogViewModel ViewModel => (ExportDialogViewModel)DataContext!;

    public ExportDialog()
        : this(new ExportDialogViewModel())
    {
    }

    public ExportDialog(ExportDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        if (this.FindControl<StackPanel>("ToastContainer") is { } toastContainer)
        {
            _previousToastContainer = ToastService.SetContainer(toastContainer);
        }

        ViewModel.CloseRequested += OnCloseRequested;
        Opened += OnDialogOpened;
        Closed += OnDialogClosed;
    }

    private async void OnDialogOpened(object? sender, EventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        Close(ViewModel.DialogResult);
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        Opened -= OnDialogOpened;
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

    private async void OnBrowseOutputDirectoryClick(object? sender, RoutedEventArgs e)
    {
        await BrowseOutputDirectoryAsync();
    }

    private async Task BrowseOutputDirectoryAsync()
    {
        if (StorageProvider == null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择导出目录",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            return;
        }

        var localPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            ToastService.ShowWarning("当前只支持导出到本地文件夹。", 3000);
            return;
        }

        ViewModel.OutputDirectory = localPath;
    }
}
