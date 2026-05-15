using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseTreeSkeletonBuilderTests
{
    [Fact]
    public void BuildSkeleton_creates_lazy_schema_object_folders_without_leaf_objects()
    {
        var schemas = new[]
        {
            new SchemaModel { Name = "public", Owner = "postgres", IsDefault = true }
        };

        var root = DatabaseTreeSkeletonBuilder.BuildSkeleton("demo", schemas);

        var schemasFolder = Assert.Single(root.Children);
        var schemaNode = Assert.Single(schemasFolder.Children);
        Assert.Equal(TreeNodeType.Schema, schemaNode.NodeType);

        Assert.Collection(
            schemaNode.Children,
            tables =>
            {
                Assert.Equal("Tables", tables.Name);
                Assert.Equal(TreeNodeLazyLoadKind.Tables, tables.LazyLoadKind);
                Assert.False(tables.IsChildrenLoaded);
                Assert.Empty(tables.Children);
            },
            views =>
            {
                Assert.Equal("Views", views.Name);
                Assert.Equal(TreeNodeLazyLoadKind.Views, views.LazyLoadKind);
                Assert.False(views.IsChildrenLoaded);
                Assert.Empty(views.Children);
            },
            procedures =>
            {
                Assert.Equal("Stored Procedures", procedures.Name);
                Assert.Equal(TreeNodeLazyLoadKind.StoredProcedures, procedures.LazyLoadKind);
                Assert.False(procedures.IsChildrenLoaded);
                Assert.Empty(procedures.Children);
            });
    }
}
