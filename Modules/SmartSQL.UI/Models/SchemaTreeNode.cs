using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartSQL.UI.Models;

/// <summary>
/// Schema 树节点
/// </summary>
public partial class SchemaTreeNode : TreeNodeItem
{
    /// <summary>
    /// Schema 名称
    /// </summary>
    [ObservableProperty]
    private string _schemaName;

    /// <summary>
    /// 表数量
    /// </summary>
    [ObservableProperty]
    private int _tableCount;

    /// <summary>
    /// 是否为默认 Schema
    /// </summary>
    [ObservableProperty]
    private bool _isDefault;

    /// <summary>
    /// 关联的 Schema 模型
    /// </summary>
    public SchemaModel? SchemaModel { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public SchemaTreeNode() : base()
    {
        _schemaName = string.Empty;
        _tableCount = 0;
        _isDefault = false;
        NodeType = TreeNodeType.Schema;
        Icon = "Schema";
        DisplayName = "Schema";
    }

    /// <summary>
    /// 构造函数（带参数）
    /// </summary>
    public SchemaTreeNode(SchemaModel schema) : this()
    {
        if (schema != null)
        {
            _schemaName = schema.Name;
            DisplayName = schema.Name;
            _tableCount = schema.TableCount;
            _isDefault = schema.IsDefault;
            SchemaModel = schema;
            Name = schema.Name;
            Icon = _isDefault ? "SchemaStar" : "Schema";
        }
    }

    /// <summary>
    /// 更新表数量
    /// </summary>
    public void UpdateTableCount()
    {
        TableCount = Children.Count(c => c.NodeType == TreeNodeType.Table);
        DisplayName = $"{SchemaName} ({TableCount})";
    }
}
