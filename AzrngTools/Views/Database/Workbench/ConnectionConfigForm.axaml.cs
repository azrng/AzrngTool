using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Markup.Xaml;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class ConnectionConfigForm : UserControl
{
    public ConnectionConfigForm()
    {
        InitializeComponent();
    }

    private async void BrowseSQLiteFile_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider == null || DataContext is not ConnectionDialogViewModel viewModel)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 Sqlite 数据库文件",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Sqlite 数据库")
                {
                    Patterns = new[] { "*.db", "*.Sqlite", "*.sqlite3" }
                },
                FilePickerFileTypes.All
            }
        });

        if (files.Count != 1)
        {
            return;
        }

        var localPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        viewModel.ConnectionConfig.Database = localPath;
    }
}
