using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using AzrngTools.ViewModels.Database;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class MainWindowViewModelTests
{
    [Fact]
    public void Database_search_filters_available_databases_without_changing_selection()
    {
        var viewModel = new MainWindowViewModel
        {
            SelectedDatabaseName = "cdr_stage1"
        };
        viewModel.AvailableDatabases.Add("cdr_stage1");
        viewModel.AvailableDatabases.Add("cdr_test_100");
        viewModel.AvailableDatabases.Add("cdss_2_1_0");
        viewModel.AvailableDatabases.Add("cdss_bdy");

        viewModel.DatabaseSearchText = "test";

        Assert.Equal(["cdr_test_100"], viewModel.FilteredAvailableDatabases);
        Assert.Equal("cdr_stage1", viewModel.SelectedDatabaseName);
    }

    [Fact]
    public void Database_search_replaces_filtered_database_collection()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AvailableDatabases.Add("cdr_stage1");
        viewModel.AvailableDatabases.Add("cdr_test_100");
        var originalCollection = viewModel.FilteredAvailableDatabases;
        var propertyChangedNames = new List<string?>();
        viewModel.PropertyChanged += (_, args) => propertyChangedNames.Add(args.PropertyName);

        viewModel.DatabaseSearchText = "test";

        Assert.NotSame(originalCollection, viewModel.FilteredAvailableDatabases);
        Assert.Equal(["cdr_test_100"], viewModel.FilteredAvailableDatabases);
        Assert.Contains(nameof(MainWindowViewModel.FilteredAvailableDatabases), propertyChangedNames);
    }

    [Fact]
    public void Connection_labels_show_only_the_selected_database_name()
    {
        var databaseContextCoordinator = new InMemoryDatabaseContextCoordinator();
        var viewModel = new MainWindowViewModel(
            new DatabaseService(),
            databaseContextManager: databaseContextCoordinator)
        {
            SelectedConnection = new ConnectionConfig { Name = "localhost-pgsql" },
            SelectedDatabaseName = "vector_dev"
        };

        Assert.Equal("vector_dev", viewModel.CurrentConnectionLabel);
        Assert.Equal("vector_dev", viewModel.CompactConnectionLabel);
        Assert.Equal(1, databaseContextCoordinator.InitializeConnectionContextCallCount);
    }

    [Fact]
    public async Task Opening_pg_dump_uses_the_active_connection_context_instead_of_the_selected_connection_default()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(MainWindowViewModelTestApplication));
        await session.Dispatch(() =>
        {
            var databaseContextCoordinator = new InMemoryDatabaseContextCoordinator();
            var activeConnection = new ConnectionConfig
            {
                Name = "当前连接",
                Database = "current_database"
            };
            var defaultConnection = new ConnectionConfig
            {
                Name = "默认连接",
                Database = "default_database"
            };
            var pgDumpExportCoordinator = new RecordingPgDumpExportCoordinator();
            var viewModel = new MainWindowViewModel(
                new DatabaseService(),
                databaseContextManager: databaseContextCoordinator,
                pgDumpExportCoordinator: pgDumpExportCoordinator)
            {
                MainWindow = new Window(),
                SelectedConnection = defaultConnection
            };
            databaseContextCoordinator.ResetConnectionContextState(activeConnection);
            databaseContextCoordinator.SelectedDatabaseName = activeConnection.Database;

            viewModel.OpenPgDumpDialogCommand.Execute(null);

            Assert.Same(activeConnection, pgDumpExportCoordinator.Connection);
            Assert.Equal(activeConnection.Database, pgDumpExportCoordinator.DatabaseName);
        }, CancellationToken.None);
    }

    [Fact]
    public void Activating_table_folder_closes_previous_table_detail()
    {
        var viewModel = new MainWindowViewModel
        {
            ShowOverviewPage = false,
            CurrentWorkspaceMode = DetailWorkspaceMode.Table
        };
        viewModel.TableDetailViewModel.Tables.Add(new TableModel { Name = "ai_mapdata", Schema = "mdm" });
        viewModel.TableDetailViewModel.SelectedTable = viewModel.TableDetailViewModel.Tables[0];
        viewModel.TableDetailViewModel.ShowObjectList = false;

        viewModel.ActivateWorkspaceFolder("Tables");

        Assert.True(viewModel.ShowTableWorkspace);
        Assert.True(viewModel.TableDetailViewModel.ShowObjectList);
        Assert.Null(viewModel.TableDetailViewModel.SelectedTable);
    }

    private sealed class InMemoryDatabaseContextCoordinator : IDatabaseContextCoordinator
    {
        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add { }
            remove { }
        }

        public int InitializeConnectionContextCallCount { get; private set; }

        public ObservableCollection<string> AvailableDatabases { get; } = [];

        public ObservableCollection<string> FilteredAvailableDatabases { get; } = [];

        public string DatabaseSearchText { get; set; } = string.Empty;

        public string? SelectedDatabaseName { get; set; }

        public ConnectionConfig? ActiveConnectionContext { get; private set; }

        public ObservableCollection<SchemaModel> Schemas { get; } = [];

        public string? CurrentSchemaName { get; set; }

        public SchemaModel? CurrentSchema { get; set; }

        public bool HasAvailableDatabases => AvailableDatabases.Count > 0;

        public Task InitializeConnectionContextAsync(ConnectionConfig connection)
        {
            InitializeConnectionContextCallCount++;
            ActiveConnectionContext = connection;
            return Task.CompletedTask;
        }

        public void ResetConnectionContextState(ConnectionConfig? nextConnection) => ActiveConnectionContext = nextConnection;

        public Task SwitchDatabaseAsync() => Task.CompletedTask;

        public Task LoadSchemasAsync(ConnectionConfig config) => Task.CompletedTask;

        public Task SelectSchemaAsync(SchemaModel schema) => Task.CompletedTask;

        public void ApplySchemaContext(string? schemaName) => CurrentSchemaName = schemaName;

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

    private sealed class RecordingPgDumpExportCoordinator : IPgDumpExportCoordinator
    {
        public ConnectionConfig? Connection { get; private set; }

        public string? DatabaseName { get; private set; }

        public Task OpenPgDumpDialogAsync(
            Window ownerWindow,
            ConnectionConfig connection,
            string? databaseName,
            Action<bool>? setLoading = null,
            Action<string?>? setLoadingText = null)
        {
            Connection = connection;
            DatabaseName = databaseName;
            return Task.CompletedTask;
        }
    }

    private sealed class MainWindowViewModelTestApplication : Application
    {
    }

}
