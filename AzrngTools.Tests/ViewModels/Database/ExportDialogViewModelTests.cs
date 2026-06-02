using System.Reflection;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.ViewModels.Database;

public class ExportDialogViewModelTests
{
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
