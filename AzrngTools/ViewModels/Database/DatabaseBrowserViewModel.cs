using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;
using AzrngTools.Utils;

namespace AzrngTools.ViewModels.Database;

/// <summary>
/// 数据库浏览 ViewModel
/// </summary>
public partial class DatabaseBrowserViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService = new();
    private readonly DebouncedActionDispatcher _searchDebouncer = new(TimeSpan.FromMilliseconds(300));
    private List<TreeNodeItem> _allNodes = new();
    private ObservableCollection<TreeNodeItem>? _subscribedRootNodes;
    private NotifyCollectionChangedEventHandler? _rootNodesChangedHandler;

    /// <summary>
    /// 根节点集合（树形结构）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TreeNodeItem> _rootNodes = new();

    /// <summary>
    /// 第一个根节点（用于 XAML 绑定）
    /// </summary>
    public TreeNodeItem? FirstRootNode => RootNodes.Count > 0 ? RootNodes[0] : null;

    /// <summary>
    /// RootNodes 集合变化处理
    /// </summary>
    partial void OnRootNodesChanged(ObservableCollection<TreeNodeItem> value)
    {
        if (_subscribedRootNodes != null && _rootNodesChangedHandler != null)
        {
            _subscribedRootNodes.CollectionChanged -= _rootNodesChangedHandler;
        }

        _subscribedRootNodes = value;
        _rootNodesChangedHandler = (s, e) => OnPropertyChanged(nameof(FirstRootNode));
        value.CollectionChanged += _rootNodesChangedHandler;

        OnPropertyChanged(nameof(FirstRootNode));
        CollectAllNodes();
    }

    /// <summary>
    /// 过滤后的根节点集合（用于搜索）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<TreeNodeItem> _filteredRootNodes = new();

    /// <summary>
    /// 当前选中的节点
    /// </summary>
    [ObservableProperty]
    private TreeNodeItem? _selectedNode;

    /// <summary>
    /// 搜索文本
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 加载状态文本
    /// </summary>
    [ObservableProperty]
    private string? _loadingText;

    /// <summary>
    /// 当前连接的配置
    /// </summary>
    [ObservableProperty]
    private ConnectionConfig? _currentConnection;

    /// <summary>
    /// 是否有搜索结果
    /// </summary>
    [ObservableProperty]
    private bool _hasSearchResults;

    /// <summary>
    /// 搜索结果数量
    /// </summary>
    [ObservableProperty]
    private int _searchResultCount;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DatabaseBrowserViewModel()
    {
    }

    /// <summary>
    /// 加载数据库树形结构命令
    /// </summary>
    [RelayCommand]
    private async Task DoLoadDataAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// 加载数据库树形结构（public 方法）
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (CurrentConnection == null)
        {
            System.Diagnostics.Debug.WriteLine("未选择数据库连接");
            return;
        }

        IsLoading = true;
        LoadingText = $"正在加载 {CurrentConnection.Name} 的数据库对象...";
        RootNodes.Clear();
        FilteredRootNodes.Clear();
        _allNodes.Clear();
        OnPropertyChanged(nameof(FirstRootNode));

        try
        {
            // 使用 DatabaseService 加载树形结构
            if (CurrentConnection.DatabaseType == DatabaseType.MySql)
            {
                var mySqlRootNode = BuildMySqlTreeSkeleton(CurrentConnection);
                RootNodes.Add(mySqlRootNode);
                OnPropertyChanged(nameof(FirstRootNode));
                CollectAllNodes();
                LoadingText = $"Loaded MySql objects for {CurrentConnection.Database}.";
                return;
            }
            var (success, rootNode, message) = await _databaseService.LoadDatabaseTreeAsync(CurrentConnection);

            if (success && rootNode != null)
            {
                RootNodes.Add(rootNode);
                OnPropertyChanged(nameof(FirstRootNode));
                CollectAllNodes();
                LoadingText = message;
                System.Diagnostics.Debug.WriteLine(message);
            }
            else
            {
                LoadingText = $"加载失败：{message}";
                System.Diagnostics.Debug.WriteLine($"加载数据库树失败：{message}");
            }
        }
        catch (Exception ex)
        {
            LoadingText = $"加载异常：{ex.Message}";
            System.Diagnostics.Debug.WriteLine($"加载数据库树异常：{ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Reset()
    {
        RootNodes.Clear();
        FilteredRootNodes.Clear();
        _allNodes.Clear();
        SelectedNode = null;
        SearchText = string.Empty;
        IsLoading = false;
        LoadingText = null;
        CurrentConnection = null;
        HasSearchResults = false;
        SearchResultCount = 0;
        OnPropertyChanged(nameof(FirstRootNode));
    }

    private TreeNodeItem BuildMySqlTreeSkeleton(ConnectionConfig connection)
    {
        var schemaName = string.IsNullOrWhiteSpace(connection.Database)
            ? "default"
            : connection.Database;
        var schemas = new[]
        {
            new SchemaModel
            {
                Name = schemaName,
                Owner = "MySql",
                IsDefault = true
            }
        };

        var rootNode = DatabaseTreeSkeletonBuilder.BuildSkeleton(connection.Name, schemas);
        var schemaNode = rootNode.Children.FirstOrDefault()?.Children.FirstOrDefault();
        if (schemaNode?.Data is SchemaModel schema)
        {
            schemaNode.AddChild(new TreeNodeItem("Functions", TreeNodeType.Folder, "Folder")
            {
                DisplayName = "函数",
                Data = schema,
                LazyLoadKind = TreeNodeLazyLoadKind.Functions,
                IsChildrenLoaded = false
            });
        }

        return rootNode;
    }

    public async Task EnsureNodeChildrenLoadedAsync(TreeNodeItem? node)
    {
        if (node == null ||
            node.LazyLoadKind == TreeNodeLazyLoadKind.None ||
            node.IsChildrenLoaded ||
            CurrentConnection == null)
        {
            return;
        }

        if (node.Data is not SchemaModel schema || string.IsNullOrWhiteSpace(schema.Name))
        {
            return;
        }

        node.IsLoading = true;
        try
        {
            var loaded = node.LazyLoadKind switch
            {
                TreeNodeLazyLoadKind.Tables => await LoadTableNodesAsync(node, schema.Name),
                TreeNodeLazyLoadKind.Views => await LoadViewNodesAsync(node, schema.Name),
                TreeNodeLazyLoadKind.StoredProcedures => await LoadProcedureNodesAsync(node, schema.Name),
                TreeNodeLazyLoadKind.Functions => await LoadFunctionNodesAsync(node, schema.Name),
                _ => false
            };

            if (loaded)
            {
                node.IsChildrenLoaded = true;
                CollectAllNodes();
            }
        }
        finally
        {
            node.IsLoading = false;
        }
    }

    private async Task<bool> LoadTableNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetTablesAsync(CurrentConnection!, schemaName);
        if (!result.Success)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        foreach (var table in result.Tables.OrderBy(table => table.Name))
        {
            folderNode.AddChild(new TreeNodeItem(table.Name, TreeNodeType.Table, "Table")
            {
                DisplayName = table.Name,
                Data = table
            });
        }

        folderNode.DisplayName = $"表 ({result.Tables.Count})";
        return true;
    }

    private async Task<bool> LoadViewNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetViewsAsync(CurrentConnection!, schemaName);
        if (!result.Success)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        foreach (var view in result.Views.OrderBy(view => view.Name))
        {
            folderNode.AddChild(new TreeNodeItem(view.Name, TreeNodeType.View, "View")
            {
                DisplayName = view.Name,
                Data = view
            });
        }

        folderNode.DisplayName = $"视图 ({result.Views.Count})";
        return true;
    }

    private async Task<bool> LoadProcedureNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetStoredProceduresAsync(CurrentConnection!, schemaName);
        if (!result.Success)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        foreach (var procedure in result.Procedures.OrderBy(procedure => procedure.Name))
        {
            folderNode.AddChild(new TreeNodeItem(procedure.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
            {
                DisplayName = procedure.Name,
                Data = procedure
            });
        }

        folderNode.DisplayName = $"存储过程 ({result.Procedures.Count})";
        return true;
    }

    private async Task<bool> LoadFunctionNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetFunctionsAsync(CurrentConnection!, schemaName);
        if (!result.Success)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        foreach (var function in result.Functions.OrderBy(function => function.Name))
        {
            folderNode.AddChild(new TreeNodeItem(function.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
            {
                DisplayName = function.Name,
                Data = function
            });
        }

        folderNode.DisplayName = $"函数 ({result.Functions.Count})";
        return true;
    }
    [RelayCommand]
    private void ToggleNode(TreeNodeItem? node)
    {
        if (node != null)
        {
            node.IsExpanded = !node.IsExpanded;
        }
    }

    /// <summary>
    /// 展开所有节点命令
    /// </summary>
    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var node in _allNodes)
        {
            node.IsExpanded = true;
        }
    }

    /// <summary>
    /// 折叠所有节点命令
    /// </summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in _allNodes)
        {
            node.IsExpanded = false;
        }
    }

    /// <summary>
    /// 展开当前节点命令
    /// </summary>
    [RelayCommand]
    private void ExpandNode(TreeNodeItem? node)
    {
        if (node != null)
        {
            node.IsExpanded = true;
        }
    }

    /// <summary>
    /// 折叠当前节点命令
    /// </summary>
    [RelayCommand]
    private void CollapseNode(TreeNodeItem? node)
    {
        if (node != null)
        {
            node.IsExpanded = false;
        }
    }

    /// <summary>
    /// 搜索对象命令
    /// </summary>
    [RelayCommand]
    private void SearchObjects()
    {
        FilterNodes(SearchText);
    }

    /// <summary>
    /// 清除搜索命令
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        FilterNodes(string.Empty);
    }

    /// <summary>
    /// 刷新命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentConnection != null)
        {
            await LoadDataAsync();
        }
    }

    /// <summary>
    /// 搜索文本变化处理
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        _searchDebouncer.Debounce(() => FilterNodes(value));
    }

    /// <summary>
    /// 过滤节点
    /// </summary>
    private void FilterNodes(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            // 显示所有节点
            FilteredRootNodes.Clear();
            if (RootNodes.Count > 0)
            {
                foreach (var node in RootNodes[0].Children)
                {
                    FilteredRootNodes.Add(node);
                }
            }

            // 恢复所有节点展开状态
            foreach (var node in _allNodes)
            {
                node.IsExpanded = true;
            }

            HasSearchResults = false;
            SearchResultCount = 0;
        }
        else
        {
            // 过滤节点
            FilteredRootNodes.Clear();
            if (RootNodes.Count > 0)
            {
                var filteredNodes = SearchNodes(RootNodes[0].Children, searchText.Trim());
                foreach (var node in filteredNodes)
                {
                    FilteredRootNodes.Add(node);
                }
            }

            // 展开所有匹配的父节点
            ExpandMatchingNodes();

            HasSearchResults = FilteredRootNodes.Count > 0;
            SearchResultCount = CountMatchingNodes(FilteredRootNodes);
        }
    }

    /// <summary>
    /// 递归搜索节点
    /// </summary>
    private List<TreeNodeItem> SearchNodes(ObservableCollection<TreeNodeItem> nodes, string searchText)
    {
        var result = new List<TreeNodeItem>();

        foreach (var node in nodes)
        {
            var matchesSearch = node.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                (node.Data?.ToString()?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);

            var hasMatchingChildren = false;
            if (node.Children.Count > 0)
            {
                var matchingChildren = SearchNodes(node.Children, searchText);
                hasMatchingChildren = matchingChildren.Count > 0;

                if (hasMatchingChildren)
                {
                    node.IsExpanded = true;
                }
            }

            if (matchesSearch || hasMatchingChildren)
            {
                result.Add(node);
            }
        }

        return result;
    }

    /// <summary>
    /// 展开所有匹配的节点
    /// </summary>
    private void ExpandMatchingNodes()
    {
        foreach (var node in FilteredRootNodes)
        {
            node.IsExpanded = true;
            ExpandNodeRecursive(node);
        }
    }

    /// <summary>
    /// 递归展开节点
    /// </summary>
    private void ExpandNodeRecursive(TreeNodeItem node)
    {
        if (node.Children.Count > 0)
        {
            node.IsExpanded = true;
            foreach (var child in node.Children)
            {
                ExpandNodeRecursive(child);
            }
        }
    }

    /// <summary>
    /// 统计搜索结果数量
    /// </summary>
    private int CountMatchingNodes(ObservableCollection<TreeNodeItem> nodes)
    {
        int count = 0;
        foreach (var node in nodes)
        {
            count++;
            count += CountMatchingNodes(node.Children);
        }
        return count;
    }

    /// <summary>
    /// 收集所有节点
    /// </summary>
    private void CollectAllNodes()
    {
        _allNodes.Clear();
        if (RootNodes.Count > 0)
        {
            CollectAllNodesRecursive(RootNodes[0]);
        }
    }

    /// <summary>
    /// 递归收集所有节点
    /// </summary>
    private void CollectAllNodesRecursive(TreeNodeItem node)
    {
        _allNodes.Add(node);
        foreach (var child in node.Children)
        {
            CollectAllNodesRecursive(child);
        }
    }

}
