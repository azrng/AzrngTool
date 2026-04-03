using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Azrng.Core.Model;
using Azrng.DataAccess.DbBridge;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.ViewModels.Database;

/// <summary>
/// 数据库浏览 ViewModel
/// </summary>
public partial class DatabaseBrowserViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService = new();
    private List<TreeNodeItem> _allNodes = new();

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
        // 当 RootNodes 变化时，通知 FirstRootNode 也更新
        OnPropertyChanged(nameof(FirstRootNode));

        // 订阅集合变化事件
        if (value != null)
        {
            value.CollectionChanged += (s, e) => OnPropertyChanged(nameof(FirstRootNode));
        }

        // 收集所有节点
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
                var mySqlRootNode = await BuildMySqlTreeAsync(CurrentConnection);
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

    /// <summary>
    /// 展开/折叠节点命令
    /// </summary>
    private async Task<TreeNodeItem> BuildMySqlTreeAsync(ConnectionConfig connection)
    {
        var schemaName = string.IsNullOrWhiteSpace(connection.Database)
            ? "default"
            : connection.Database;
        var dbBridge = new MySqlBasicDbBridge(connection.ToConnectionString());

        var rootNode = new TreeNodeItem(connection.Name, TreeNodeType.Root, "Database")
        {
            DisplayName = connection.Name,
            IsExpanded = true
        };

        var schemasFolderNode = new TreeNodeItem("Schemas", TreeNodeType.Folder, "Folder")
        {
            DisplayName = "Schemas (1)",
            IsExpanded = true
        };
        rootNode.AddChild(schemasFolderNode);

        var schemaNode = new TreeNodeItem(schemaName, TreeNodeType.Schema, "Schema")
        {
            DisplayName = schemaName,
            Data = new SchemaModel
            {
                Name = schemaName,
                Owner = "MySql",
                IsDefault = true
            },
            IsExpanded = true
        };

        var tablesResult = await _databaseService.GetTablesAsync(connection, schemaName);
        if (tablesResult.Success)
        {
            foreach (var table in tablesResult.Tables.OrderBy(table => table.Name))
            {
                schemaNode.AddChild(new TreeNodeItem(table.Name, TreeNodeType.Table, "Table")
                {
                    DisplayName = table.Name,
                    Data = table
                });
            }
        }

        schemasFolderNode.AddChild(schemaNode);

        var viewsFolderNode = new TreeNodeItem("Views", TreeNodeType.Folder, "Folder")
        {
            DisplayName = "Views (0)",
            IsExpanded = false
        };

        var views = (await dbBridge.GetSchemaViewListAsync(schemaName))
            .Select(dto => new AzrngTools.Models.Database.ViewModel
            {
                Name = dto.ViewName,
                Schema = dto.ViewOwner,
                Definition = dto.ViewDefinition ?? string.Empty,
                Comment = dto.ViewDescription ?? string.Empty
            })
            .OrderBy(view => view.Name)
            .ToList();

        foreach (var view in views)
        {
            viewsFolderNode.AddChild(new TreeNodeItem(view.Name, TreeNodeType.View, "View")
            {
                DisplayName = view.Name,
                Data = view
            });
        }

        viewsFolderNode.DisplayName = $"Views ({views.Count})";

        rootNode.AddChild(viewsFolderNode);

        var proceduresFolderNode = new TreeNodeItem("Stored Procedures", TreeNodeType.Folder, "Folder")
        {
            DisplayName = "Stored Procedures (0)",
            IsExpanded = false
        };

        var routines = await dbBridge.GetSchemaRoutineListAsync(schemaName);
        var procedures = routines
            .Where(routine => string.Equals(routine.RoutineType, "PROCEDURE", StringComparison.OrdinalIgnoreCase))
            .Select(routine => new StoredProcedureModel
            {
                Name = routine.RoutineName,
                Schema = routine.SchemaName,
                Definition = routine.RoutineDefinition ?? string.Empty,
                Parameters = $"{routine.InputParam ?? string.Empty} {routine.OutputParam ?? string.Empty}".Trim(),
                Comment = routine.RoutineDescription ?? string.Empty,
                RoutineType = "PROCEDURE"
            })
            .OrderBy(procedure => procedure.Name)
            .ToList();

        foreach (var procedure in procedures)
        {
            proceduresFolderNode.AddChild(new TreeNodeItem(procedure.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
            {
                DisplayName = procedure.Name,
                Data = procedure
            });
        }

        proceduresFolderNode.DisplayName = $"Stored Procedures ({procedures.Count})";

        rootNode.AddChild(proceduresFolderNode);

        var functionsFolderNode = new TreeNodeItem("Functions", TreeNodeType.Folder, "Folder")
        {
            DisplayName = "Functions (0)",
            IsExpanded = false
        };

        var functions = routines
            .Where(routine => string.Equals(routine.RoutineType, "FUNCTION", StringComparison.OrdinalIgnoreCase))
            .Select(routine => new StoredProcedureModel
            {
                Name = routine.RoutineName,
                Schema = routine.SchemaName,
                Definition = routine.RoutineDefinition ?? string.Empty,
                Parameters = $"{routine.InputParam ?? string.Empty} {routine.OutputParam ?? string.Empty}".Trim(),
                Comment = routine.RoutineDescription ?? string.Empty,
                RoutineType = "FUNCTION"
            })
            .OrderBy(function => function.Name)
            .ToList();

        foreach (var function in functions)
        {
            functionsFolderNode.AddChild(new TreeNodeItem(function.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
            {
                DisplayName = function.Name,
                Data = function
            });
        }

        functionsFolderNode.DisplayName = $"Functions ({functions.Count})";

        rootNode.AddChild(functionsFolderNode);

        return rootNode;
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
        FilterNodes(value);
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
                var filteredNodes = SearchNodes(RootNodes[0].Children, searchText.ToLower());
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
            var matchesSearch = node.DisplayName.ToLower().Contains(searchText) ||
                               node.Name.ToLower().Contains(searchText) ||
                               (node.Data?.ToString()?.ToLower().Contains(searchText) ?? false);

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
