using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 视图树节点
/// </summary>
public partial class ViewTreeNode : TreeNodeItem
{
    /// <summary>
    /// 视图名称
    /// </summary>
    [ObservableProperty]
    private string _viewName;

    /// <summary>
    /// Schema 名称
    /// </summary>
    [ObservableProperty]
    private string _schemaName;

    /// <summary>
    /// 视图定义
    /// </summary>
    [ObservableProperty]
    private string _definition;

    /// <summary>
    /// 视图说明
    /// </summary>
    [ObservableProperty]
    private string _comment;

    /// <summary>
    /// 关联的 View 模型
    /// </summary>
    public ViewModel? ViewModel { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public ViewTreeNode() : base()
    {
        _viewName = string.Empty;
        _schemaName = string.Empty;
        _definition = string.Empty;
        _comment = string.Empty;
        NodeType = TreeNodeType.View;
        Icon = "View";
        DisplayName = "View";
    }

    /// <summary>
    /// 构造函数（带参数）
    /// </summary>
    public ViewTreeNode(ViewModel view) : this()
    {
        if (view != null)
        {
            _viewName = view.Name;
            _schemaName = view.Schema;
            _definition = view.Definition;
            _comment = view.Comment;
            Name = view.Name;
            DisplayName = string.IsNullOrEmpty(view.Comment) ? view.Name : $"{view.Name} ({view.Comment})";
            ViewModel = view;
        }
    }
}
