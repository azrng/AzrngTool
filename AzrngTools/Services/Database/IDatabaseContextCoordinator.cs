using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Azrng.Core.Model;
using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public interface IDatabaseContextCoordinator : INotifyPropertyChanged
{
    ObservableCollection<string> AvailableDatabases { get; }
    ObservableCollection<string> FilteredAvailableDatabases { get; }
    string DatabaseSearchText { get; set; }
    string? SelectedDatabaseName { get; set; }
    ConnectionConfig? ActiveConnectionContext { get; }
    ObservableCollection<SchemaModel> Schemas { get; }
    string? CurrentSchemaName { get; set; }
    SchemaModel? CurrentSchema { get; set; }
    bool HasAvailableDatabases { get; }

    Task InitializeConnectionContextAsync(ConnectionConfig connection);
    void ResetConnectionContextState(ConnectionConfig? nextConnection);
    Task SwitchDatabaseAsync();
    Task LoadSchemasAsync(ConnectionConfig config);
    Task SelectSchemaAsync(SchemaModel schema);
    void ApplySchemaContext(string? schemaName);
    void RefreshFilteredAvailableDatabases();
    void ResetWorkspaceState();
    ConnectionConfig? GetActiveConnection();
    Task LoadAvailableDatabasesAsync(ConnectionConfig connection, string? preferredDatabase);
    bool ShouldSuppressDatabaseSelectionChanged();
}
