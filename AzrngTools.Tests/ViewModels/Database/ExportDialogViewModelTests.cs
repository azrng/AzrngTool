using System.Reflection;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class ExportDialogViewModelTests
{
    [Fact]
    public void Export_dialog_connection_summary_shows_only_the_database_name()
    {
        var viewModel = new ExportDialogViewModel(
            new ConnectionConfig { Name = "localhost-pgsql" },
            "vector_dev",
            preferredSchemaName: null,
            initialOutputDirectory: null,
            new DatabaseService());

        Assert.Equal("vector_dev", viewModel.ConnectionSummary);
    }

    [Fact]
    public void Pg_dump_connection_summary_shows_the_selected_database_name()
    {
        var viewModel = new PgDumpExportDialogViewModel(
            new ConnectionConfig { Name = "localhost-pgsql", Host = "localhost", Port = 5432 },
            "vector_dev",
            new DatabaseService(),
            lastExePath: null)
        {
            SelectedDatabase = "vector_dev"
        };

        Assert.Equal("vector_dev", viewModel.ConnectionSummary);
    }

    [Fact]
    public void Pg_dump_connection_summary_falls_back_to_the_connection_name_when_no_database_is_selected()
    {
        var viewModel = new PgDumpExportDialogViewModel(
            new ConnectionConfig { Name = "localhost-pgsql", Database = "postgres" },
            "vector_dev",
            new DatabaseService(),
            lastExePath: null);

        Assert.Equal("localhost-pgsql", viewModel.ConnectionSummary);
    }

    [Theory]
    [InlineData(TreeNodeType.Table, ExportObjectType.Table)]
    [InlineData(TreeNodeType.View, ExportObjectType.View)]
    [InlineData(TreeNodeType.StoredProcedure, ExportObjectType.Procedure)]
    public void Map_export_object_type_supports_exportable_object_nodes(
        TreeNodeType nodeType,
        ExportObjectType expectedObjectType)
    {
        var method = typeof(ExportDialogViewModel).GetMethod(
            "MapExportObjectType",
            BindingFlags.Static | BindingFlags.NonPublic);

        var actualObjectType = Assert.IsType<ExportObjectType>(method?.Invoke(null, [nodeType]));

        Assert.Equal(expectedObjectType, actualObjectType);
    }
}
