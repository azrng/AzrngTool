using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Azrng.Core.Model;
using Azrng.Core.Results;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.ViewModels.Database;

/// <summary>
/// 数据库浏览 ViewModel。
/// 注意：对象树的搜索过滤由 <see cref="Controls.Database.DatabaseTree"/> 控件内置实现
/// （绑定其 SearchText），本 VM 不再维护独立的过滤集合与搜索逻辑。
/// </summary>
public partial class DatabaseBrowserViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;
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
    }

    /// <summary>
    /// 当前选中的节点
    /// </summary>
    [ObservableProperty]
    private TreeNodeItem? _selectedNode;

    /// <summary>
    /// 搜索文本（仅作为状态保留；实际过滤由 DatabaseTree 控件处理）
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
    /// 构造函数
    /// </summary>
    public DatabaseBrowserViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
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
        OnPropertyChanged(nameof(FirstRootNode));

        try
        {
            // 使用 DatabaseService 加载树形结构
            if (CurrentConnection.DatabaseType == DatabaseType.MySql)
            {
                var mySqlRootNode = BuildMySqlTreeSkeleton(CurrentConnection);
                RootNodes.Add(mySqlRootNode);
                OnPropertyChanged(nameof(FirstRootNode));
                LoadingText = $"Loaded MySql objects for {CurrentConnection.Database}.";
                return;
            }
            var result = await _databaseService.LoadDatabaseTreeAsync(CurrentConnection);

            if (result.IsSuccess && result.Data != null)
            {
                RootNodes.Add(result.Data);
                OnPropertyChanged(nameof(FirstRootNode));
                LoadingText = result.Message;
                System.Diagnostics.Debug.WriteLine(result.Message);
            }
            else
            {
                LoadingText = $"加载失败：{result.Message}";
                System.Diagnostics.Debug.WriteLine($"加载数据库树失败：{result.Message}");
            }
        }
        catch (Exception ex)
        {
            LoadingText = $"加载异常：{ex.Message}";
            LoggingService.LogError($"加载数据库树异常: {CurrentConnection?.Name}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Reset()
    {
        RootNodes.Clear();
        SelectedNode = null;
        SearchText = string.Empty;
        IsLoading = false;
        LoadingText = null;
        CurrentConnection = null;
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
        if (!result.IsSuccess)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        var tables = result.DataOrEmpty();
        foreach (var table in tables.OrderBy(table => table.Name))
        {
            folderNode.AddChild(new TreeNodeItem(table.Name, TreeNodeType.Table, "Table")
            {
                DisplayName = table.Name,
                Data = table
            });
        }

        folderNode.DisplayName = $"表 ({tables.Count})";
        return true;
    }

    private async Task<bool> LoadViewNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetViewsAsync(CurrentConnection!, schemaName);
        if (!result.IsSuccess)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        var views = result.DataOrEmpty();
        foreach (var view in views.OrderBy(view => view.Name))
        {
            folderNode.AddChild(new TreeNodeItem(view.Name, TreeNodeType.View, "View")
            {
                DisplayName = view.Name,
                Data = view
            });
        }

        folderNode.DisplayName = $"视图 ({views.Count})";
        return true;
    }

    private async Task<bool> LoadProcedureNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetStoredProceduresAsync(CurrentConnection!, schemaName);
        if (!result.IsSuccess)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        var procedures = result.DataOrEmpty();
        foreach (var procedure in procedures.OrderBy(procedure => procedure.Name))
        {
            folderNode.AddChild(new TreeNodeItem(procedure.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
            {
                DisplayName = procedure.Name,
                Data = procedure
            });
        }

        folderNode.DisplayName = $"存储过程 ({procedures.Count})";
        return true;
    }

    private async Task<bool> LoadFunctionNodesAsync(TreeNodeItem folderNode, string schemaName)
    {
        var result = await _databaseService.GetFunctionsAsync(CurrentConnection!, schemaName);
        if (!result.IsSuccess)
        {
            LoadingText = result.Message;
            return false;
        }

        folderNode.ClearChildren();
        var functions = result.DataOrEmpty();
        foreach (var function in functions.OrderBy(function => function.Name))
        {
            folderNode.AddChild(new TreeNodeItem(function.Name, TreeNodeType.StoredProcedure, "StoredProcedure")
            {
                DisplayName = function.Name,
                Data = function
            });
        }

        folderNode.DisplayName = $"函数 ({functions.Count})";
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
}
