using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 树节点类型枚举
/// </summary>
public enum TreeNodeType
{
    /// <summary>
    /// 根节点
    /// </summary>
    Root,

    /// <summary>
    /// 数据库节点
    /// </summary>
    Database,

    /// <summary>
    /// Schema 节点
    /// </summary>
    Schema,

    /// <summary>
    /// 表节点
    /// </summary>
    Table,

    /// <summary>
    /// 视图节点
    /// </summary>
    View,

    /// <summary>
    /// 存储过程节点
    /// </summary>
    StoredProcedure,

    /// <summary>
    /// 列节点
    /// </summary>
    Column,

    /// <summary>
    /// 文件夹节点
    /// </summary>
    Folder
}

public enum TreeNodeLazyLoadKind
{
    None,
    Tables,
    Views,
    StoredProcedures,
    Functions
}

/// <summary>
/// 树形节点基类
/// </summary>
public partial class TreeNodeItem : ObservableObject
{
    /// <summary>
    /// 节点名称
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// 显示名称
    /// </summary>
    [ObservableProperty]
    private string _displayName;

    /// <summary>
    /// 节点图标
    /// </summary>
    [ObservableProperty]
    private string _icon;

    public string IconGlyph => NodeType switch
    {
        TreeNodeType.Root => "🗃",
        TreeNodeType.Database => "🗃",
        TreeNodeType.Folder => "📁",
        TreeNodeType.Schema => "◪",
        TreeNodeType.Table => "▦",
        TreeNodeType.View => "◫",
        TreeNodeType.StoredProcedure => "⚙",
        TreeNodeType.Column => "•",
        _ => "•"
    };

    /// <summary>
    /// 节点类型
    /// </summary>
    [ObservableProperty]
    private TreeNodeType _nodeType;

    /// <summary>
    /// 是否展开
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// 是否选中
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool? _isExportChecked;

    [ObservableProperty]
    private bool _showExportCheckBox;

    /// <summary>
    /// 是否加载中
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isChildrenLoaded = true;

    [ObservableProperty]
    private TreeNodeLazyLoadKind _lazyLoadKind;

    /// <summary>
    /// 父节点
    /// </summary>
    public TreeNodeItem? Parent { get; set; }

    /// <summary>
    /// 子节点集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TreeNodeItem> _children = new();

    /// <summary>
    /// 是否有子节点
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// 是否可以展开
    /// </summary>
    public bool CanExpand => HasChildren || LazyLoadKind != TreeNodeLazyLoadKind.None || NodeType == TreeNodeType.Schema || NodeType == TreeNodeType.Database;

    public bool IsExportableLeaf => NodeType == TreeNodeType.Table;

    /// <summary>
    /// 关联的数据对象
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public TreeNodeItem()
    {
        _name = string.Empty;
        _displayName = string.Empty;
        _icon = "Folder";
        _nodeType = TreeNodeType.Folder;
        _children = new ObservableCollection<TreeNodeItem>();
        _children.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasChildren));
    }

    /// <summary>
    /// 构造函数（带参数）
    /// </summary>
    public TreeNodeItem(string name, TreeNodeType nodeType, string icon = "Folder") : this()
    {
        _name = name;
        _displayName = name;
        _nodeType = nodeType;
        _icon = icon;
    }

    partial void OnNodeTypeChanged(TreeNodeType value)
    {
        OnPropertyChanged(nameof(IconGlyph));
    }

    /// <summary>
    /// 添加子节点
    /// </summary>
    public void AddChild(TreeNodeItem child)
    {
        if (child != null)
        {
            child.Parent = this;
            Children.Add(child);
        }
    }

    /// <summary>
    /// 移除子节点
    /// </summary>
    public void RemoveChild(TreeNodeItem child)
    {
        if (child != null)
        {
            child.Parent = null;
            Children.Remove(child);
        }
    }

    /// <summary>
    /// 清除所有子节点
    /// </summary>
    public void ClearChildren()
    {
        foreach (var child in Children)
        {
            child.Parent = null;
        }
        Children.Clear();
    }

    /// <summary>
    /// 获取所有后代节点（递归）
    /// </summary>
    public IEnumerable<TreeNodeItem> GetAllDescendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var descendant in child.GetAllDescendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// 获取根节点
    /// </summary>
    public TreeNodeItem GetRoot()
    {
        var current = this;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    /// <summary>
    /// 获取路径（从根到当前节点）
    /// </summary>
    public List<TreeNodeItem> GetPath()
    {
        var path = new List<TreeNodeItem>();
        var current = this;
        while (current != null)
        {
            path.Insert(0, current);
            current = current.Parent;
        }
        return path;
    }
}
