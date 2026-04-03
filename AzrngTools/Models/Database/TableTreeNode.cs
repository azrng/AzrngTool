using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 表树节点
/// </summary>
public partial class TableTreeNode : TreeNodeItem
{
    /// <summary>
    /// 表名称
    /// </summary>
    [ObservableProperty]
    private string _tableName;

    /// <summary>
    /// Schema 名称
    /// </summary>
    [ObservableProperty]
    private string _schemaName;

    /// <summary>
    /// 表类型
    /// </summary>
    [ObservableProperty]
    private string _tableType;

    /// <summary>
    /// 表说明
    /// </summary>
    [ObservableProperty]
    private string _comment;

    /// <summary>
    /// 列数量
    /// </summary>
    [ObservableProperty]
    private int _columnCount;

    /// <summary>
    /// 关联的 Table 模型
    /// </summary>
    public TableModel? TableModel { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public TableTreeNode() : base()
    {
        _tableName = string.Empty;
        _schemaName = string.Empty;
        _tableType = "TABLE";
        _comment = string.Empty;
        _columnCount = 0;
        NodeType = TreeNodeType.Table;
        Icon = "Table";
        DisplayName = "Table";
    }

    /// <summary>
    /// 构造函数（带参数）
    /// </summary>
    public TableTreeNode(TableModel table) : this()
    {
        if (table != null)
        {
            _tableName = table.Name;
            _schemaName = table.Schema;
            _tableType = table.TableType;
            _comment = table.Comment;
            Name = table.Name;
            DisplayName = string.IsNullOrEmpty(table.Comment) ? table.Name : $"{table.Name} ({table.Comment})";
            TableModel = table;
        }
    }

    /// <summary>
    /// 更新列数量
    /// </summary>
    public void UpdateColumnCount()
    {
        ColumnCount = Children.Count;
        if (!string.IsNullOrEmpty(Comment))
        {
            DisplayName = $"{TableName} ({Comment}) [{ColumnCount}]";
        }
        else
        {
            DisplayName = $"{TableName} [{ColumnCount}]";
        }
    }
}
