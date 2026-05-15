using AzrngTools.Models.Database;

namespace AzrngTools.Services.Database;

public static class DatabaseTreeSkeletonBuilder
{
    public static TreeNodeItem BuildSkeleton(string connectionName, IEnumerable<SchemaModel> schemas)
    {
        var rootNode = new TreeNodeItem(connectionName, TreeNodeType.Root, "Database")
        {
            DisplayName = connectionName,
            IsExpanded = true
        };

        var schemaList = schemas
            .Where(schema => !string.IsNullOrWhiteSpace(schema.Name))
            .OrderByDescending(schema => schema.IsDefault)
            .ThenBy(schema => schema.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var schemasFolderNode = new TreeNodeItem("Schemas", TreeNodeType.Folder, "Folder")
        {
            DisplayName = $"架构 ({schemaList.Count})",
            IsExpanded = true
        };
        rootNode.AddChild(schemasFolderNode);

        foreach (var schema in schemaList)
        {
            var schemaNode = new TreeNodeItem(schema.Name, TreeNodeType.Schema, "Schema")
            {
                DisplayName = schema.Name,
                Data = schema,
                IsChildrenLoaded = true
            };

            schemaNode.AddChild(CreateLazyFolder("Tables", "表", TreeNodeLazyLoadKind.Tables, schema));
            schemaNode.AddChild(CreateLazyFolder("Views", "视图", TreeNodeLazyLoadKind.Views, schema));
            schemaNode.AddChild(CreateLazyFolder("Stored Procedures", "存储过程", TreeNodeLazyLoadKind.StoredProcedures, schema));
            schemasFolderNode.AddChild(schemaNode);
        }

        return rootNode;
    }

    private static TreeNodeItem CreateLazyFolder(
        string name,
        string displayName,
        TreeNodeLazyLoadKind lazyLoadKind,
        SchemaModel schema)
    {
        return new TreeNodeItem(name, TreeNodeType.Folder, "Folder")
        {
            DisplayName = displayName,
            Data = schema,
            LazyLoadKind = lazyLoadKind,
            IsChildrenLoaded = false
        };
    }
}
