using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 存储过程树节点
/// </summary>
public partial class StoredProcedureTreeNode : TreeNodeItem
{
    /// <summary>
    /// 存储过程名称
    /// </summary>
    [ObservableProperty]
    private string _procedureName;

    /// <summary>
    /// Schema 名称
    /// </summary>
    [ObservableProperty]
    private string _schemaName;

    /// <summary>
    /// 存储过程定义
    /// </summary>
    [ObservableProperty]
    private string _definition;

    /// <summary>
    /// 参数信息
    /// </summary>
    [ObservableProperty]
    private string _parameters;

    /// <summary>
    /// 存储过程说明
    /// </summary>
    [ObservableProperty]
    private string _comment;

    /// <summary>
    /// 关联的 StoredProcedure 模型
    /// </summary>
    public StoredProcedureModel? StoredProcedureModel { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public StoredProcedureTreeNode() : base()
    {
        _procedureName = string.Empty;
        _schemaName = string.Empty;
        _definition = string.Empty;
        _parameters = string.Empty;
        _comment = string.Empty;
        NodeType = TreeNodeType.StoredProcedure;
        Icon = "Procedure";
        DisplayName = "StoredProcedure";
    }

    /// <summary>
    /// 构造函数（带参数）
    /// </summary>
    public StoredProcedureTreeNode(StoredProcedureModel procedure) : this()
    {
        if (procedure != null)
        {
            _procedureName = procedure.Name;
            _schemaName = procedure.Schema;
            _definition = procedure.Definition;
            _parameters = procedure.Parameters;
            _comment = procedure.Comment;
            Name = procedure.Name;
            DisplayName = string.IsNullOrEmpty(procedure.Comment) ? procedure.Name : $"{procedure.Name} ({procedure.Comment})";
            StoredProcedureModel = procedure;
        }
    }
}
