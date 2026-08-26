using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using AzrngTools.Converters.Database;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;
using AzrngTools.Views.Database;
using AzrngTools.Views.Database.Workbench;

namespace AzrngTools.Tests.Views.Database;

[Collection("AvaloniaHeadlessRegression")]
public class DatabaseWorkbenchPageViewTests
{
    [Fact]
    public async Task Export_dialogs_can_be_attached_to_a_window_with_the_default_design_tokens()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(WorkbenchTestApplication));
        await session.Dispatch(() =>
        {
            var window = new Window { Content = new ExportDialog() };

            window.Show();

            window.Content = new PgDumpExportDialog();

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Export_dialog_presents_document_types_as_a_compact_radio_button_group()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(WorkbenchTestApplication));
        await session.Dispatch(() =>
        {
            var dialog = new ExportDialog();
            var window = new Window { Width = 960, Height = 620, Content = dialog };
            window.Show();
            dialog.DataContext = new ExportDialogViewModel();
            window.UpdateLayout();

            var formatLabel = dialog.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(textBlock => textBlock.Text == "文档格式");
            var formatGroup = formatLabel.GetVisualAncestors()
                .OfType<Border>()
                .First(border => border.Classes.Contains("exportFormGroup"));
            var documentTypeOptions = formatGroup.GetVisualDescendants()
                .OfType<RadioButton>()
                .ToArray();
            Assert.Equal(2, documentTypeOptions.Length);
            Assert.All(documentTypeOptions, option =>
            {
                Assert.Equal("ExportDocumentType", option.GroupName);
                Assert.True(option.Bounds.Width > 0, "每个格式选项应获得可点击宽度。");
                Assert.True(option.Bounds.Height > 0, "每个格式选项应完整参与运行时布局。");
            });
            Assert.Contains(documentTypeOptions, option => option.Content?.ToString() == "Excel (.xlsx)");
            Assert.Contains(documentTypeOptions, option => option.Content?.ToString() == "Markdown (.md)");
            Assert.DoesNotContain(formatGroup.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("exportDocumentTypeCard"));

            var footer = dialog.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("exportFooter"));
            var optionsBottom = documentTypeOptions.Max(option => option.TranslatePoint(
                new Point(0, option.Bounds.Height),
                dialog)!.Value.Y);
            var footerTop = footer.TranslatePoint(new Point(0, 0), dialog)!.Value.Y;
            Assert.True(
                optionsBottom <= footerTop,
                $"文档格式选项不应落入固定页脚。选项底部：{optionsBottom}，页脚顶部：{footerTop}");

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Table_detail_view_renders_a_single_full_width_detail_surface_without_the_duplicate_table_list()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(WorkbenchTestApplication));
        await session.Dispatch(() =>
        {
            var view = new TableDetailView
            {
                DataContext = new TableDetailViewModel(new DatabaseService())
            };
            var window = new Window { Content = view };
            window.Show();

            Assert.Empty(view.GetVisualDescendants().OfType<DataGrid>());
            Assert.Single(view.GetVisualDescendants().OfType<TableDetailContent>());

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task More_actions_export_menu_assigns_the_owner_when_data_context_arrives_after_attachment_and_executes_the_command()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(WorkbenchTestApplication));
        await session.Dispatch(() =>
        {
            var connection = new ConnectionConfig { Name = "测试连接" };
            var contextCoordinator = new StubDatabaseContextCoordinator
            {
                ActiveConnectionContext = connection
            };
            var exportCoordinator = new RecordingExportCoordinator();
            var viewModel = new MainWindowViewModel(
                new DatabaseService(),
                databaseContextManager: contextCoordinator,
                exportCoordinator: exportCoordinator);
            var page = new DatabaseWorkbenchPageView();
            var window = new Window { Content = page };
            window.Show();

            page.DataContext = viewModel;

            Assert.Same(window, viewModel.MainWindow);

            var moreActionsButton = page.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Flyout is MenuFlyout);
            var menuFlyout = Assert.IsType<MenuFlyout>(moreActionsButton.Flyout);
            menuFlyout.ShowAt(moreActionsButton);
            var exportMenuItem = menuFlyout.Items.OfType<MenuItem>()
                .Single(item => item.Header?.ToString() == "导出文档");
            var pgDumpMenuItem = menuFlyout.Items.OfType<MenuItem>()
                .Single(item => item.Header?.ToString() == "pg_dump 导出");

            Assert.Same(viewModel.OpenExportDialogCommand, exportMenuItem.Command);
            Assert.Same(viewModel.OpenPgDumpDialogCommand, pgDumpMenuItem.Command);

            exportMenuItem.Command!.Execute(exportMenuItem.CommandParameter);

            Assert.Equal(1, exportCoordinator.OpenCallCount);
            Assert.Same(connection, exportCoordinator.Connection);
            window.Close();
        }, CancellationToken.None);
    }

    private sealed class RecordingExportCoordinator : IExportCoordinator
    {
        public int OpenCallCount { get; private set; }

        public ConnectionConfig? Connection { get; private set; }

        public Task OpenExportDialogAsync(
            Window ownerWindow,
            ConnectionConfig connection,
            string? databaseName,
            string? schemaName,
            Action<bool>? setLoading = null,
            Action<string?>? setLoadingText = null)
        {
            OpenCallCount++;
            Connection = connection;
            return Task.CompletedTask;
        }

        public Task GenerateCodeAsync(
            Window ownerWindow,
            ConnectionConfig connection,
            string? schemaName,
            string? mode,
            Action<bool>? setLoading = null,
            Action<string?>? setLoadingText = null) => Task.CompletedTask;
    }

    private sealed class StubDatabaseContextCoordinator : IDatabaseContextCoordinator
    {
        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        public ObservableCollection<string> AvailableDatabases { get; } = [];

        public ObservableCollection<string> FilteredAvailableDatabases { get; } = [];

        public string DatabaseSearchText { get; set; } = string.Empty;

        public string? SelectedDatabaseName { get; set; }

        public ConnectionConfig? ActiveConnectionContext { get; set; }

        public ObservableCollection<SchemaModel> Schemas { get; } = [];

        public string? CurrentSchemaName { get; set; }

        public SchemaModel? CurrentSchema { get; set; }

        public bool HasAvailableDatabases => AvailableDatabases.Count > 0;

        public Task InitializeConnectionContextAsync(ConnectionConfig connection) => Task.CompletedTask;

        public void ResetConnectionContextState(ConnectionConfig? nextConnection)
        {
            ActiveConnectionContext = nextConnection;
        }

        public Task SwitchDatabaseAsync() => Task.CompletedTask;

        public Task LoadSchemasAsync(ConnectionConfig config) => Task.CompletedTask;

        public Task SelectSchemaAsync(SchemaModel schema) => Task.CompletedTask;

        public void ApplySchemaContext(string? schemaName)
        {
            CurrentSchemaName = schemaName;
        }

        public void RefreshFilteredAvailableDatabases()
        {
        }

        public void ResetWorkspaceState()
        {
        }

        public ConnectionConfig? GetActiveConnection() => ActiveConnectionContext;

        public Task LoadAvailableDatabasesAsync(ConnectionConfig connection, string? preferredDatabase) => Task.CompletedTask;

        public bool ShouldSuppressDatabaseSelectionChanged() => false;
    }

    private sealed class WorkbenchTestApplication : Application
    {
        public override void Initialize()
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri("avares://AzrngTools/"))
            {
                Source = new Uri("avares://AzrngTools/Styles/DesignTokens.axaml")
            });
            Resources["NullToBoolConverter"] = new NullToBoolConverter();
            Resources["AllTrueConverter"] = new AllTrueConverter();
            Resources["EqualToZeroConverter"] = new EqualToZeroConverter();
            Resources["InvertedBoolConverter"] = new InvertedBoolConverter();
            Styles.Add(new FluentTheme());
        }
    }
}
