using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;
using Irihi.Avalonia.Shared.Contracts;

namespace AzrngTools.ViewModels.Database;

public partial class ExportDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly ConnectionConfig _connection;
    private readonly string? _databaseName;
    private readonly DatabaseService _databaseService = new();
    private bool _isSynchronizingChecks;

    [ObservableProperty]
    private TreeNodeItem? _exportRootNode;

    [ObservableProperty]
    private string _documentName = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private ExportDocumentType _selectedDocumentType = ExportDocumentType.Excel;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private string _loadErrorMessage = string.Empty;

    public ExportDialogResultDto? DialogResult { get; private set; }

    public string ConnectionName => _connection.Name;

    public string DatabaseLabel => string.IsNullOrWhiteSpace(_databaseName)
        ? "当前数据库未指定"
        : _databaseName!;

    public string ConnectionSummary => string.IsNullOrWhiteSpace(_databaseName)
        ? _connection.Name
        : $"{_connection.Name} / {_databaseName}";

    public IReadOnlyList<TreeNodeItem> ExportRootChildren => ExportRootNode?.Children.ToList() ?? [];

    public int SelectedObjectCount => EnumerateNodes(ExportRootNode).Count(node => node.IsExportableLeaf && node.IsExportChecked == true);

    public bool HasObjects => EnumerateNodes(ExportRootNode).Any(node => node.IsExportableLeaf);

    public string SelectionSummary => SelectedObjectCount == 0
        ? "请选择导出对象"
        : $"已选择 {SelectedObjectCount} 个对象";

    public string SuggestedFileName => $"{ConnectionName}数据库设计文档{DateTime.Now:yyyyMMddHHmmss}";

    public bool IsExcelSelected
    {
        get => SelectedDocumentType == ExportDocumentType.Excel;
        set
        {
            if (value)
            {
                SelectedDocumentType = ExportDocumentType.Excel;
            }
        }
    }

    public bool IsMarkdownSelected
    {
        get => SelectedDocumentType == ExportDocumentType.Markdown;
        set
        {
            if (value)
            {
                SelectedDocumentType = ExportDocumentType.Markdown;
            }
        }
    }

    public string DocumentTypeHint => SelectedDocumentType switch
    {
        ExportDocumentType.Markdown => "Markdown 适合输出轻量结构文档，便于阅读与版本管理。",
        _ => "Excel 适合结构化整理表结构信息。"
    };

    public ExportDialogViewModel()
        : this(
            new ConnectionConfig
            {
                Name = "pgsql",
                Database = "chat",
                DatabaseType = DatabaseType.PostgresSql
            },
            "chat",
            null)
    {
        var root = new TreeNodeItem("pgsql", TreeNodeType.Root, "Database")
        {
            DisplayName = "pgsql",
            IsExpanded = true
        };

        var schemasNode = new TreeNodeItem("Schemas", TreeNodeType.Folder, "Folder")
        {
            DisplayName = "架构 (1)",
            IsExpanded = true
        };
        root.AddChild(schemasNode);

        var schemaNode = new TreeNodeItem("chat", TreeNodeType.Schema, "Schema")
        {
            DisplayName = "chat",
            IsExpanded = true
        };
        schemasNode.AddChild(schemaNode);
        schemaNode.AddChild(new TreeNodeItem("chathistory", TreeNodeType.Table, "Table") { DisplayName = "chathistory" });
        schemaNode.AddChild(new TreeNodeItem("groupinfo", TreeNodeType.Table, "Table") { DisplayName = "groupinfo" });

        ExportRootNode = root;
        PrepareExportTree(root);
        NotifyExportTreeChanged();
    }

    public ExportDialogViewModel(ConnectionConfig connection, string? databaseName, string? preferredSchemaName, string? initialOutputDirectory = null)
    {
        _connection = connection;
        _databaseName = databaseName;
        DocumentName = SuggestedFileName;
        OutputDirectory = ResolveInitialOutputDirectory(initialOutputDirectory);
    }

    public async Task InitializeAsync()
    {
        if (IsLoading || HasObjects)
        {
            return;
        }

        IsLoading = true;
        LoadingText = "正在加载导出对象...";
        HasLoadError = false;
        LoadErrorMessage = string.Empty;

        try
        {
            var rootNode = await LoadExportTreeAsync();
            ExportRootNode = rootNode;
            PrepareExportTree(rootNode);
            NotifyExportTreeChanged();
        }
        catch (Exception ex)
        {
            HasLoadError = true;
            LoadErrorMessage = ex.Message;
            LoggingService.LogError("Failed to initialize export dialog.", ex);
        }
        finally
        {
            IsLoading = false;
            LoadingText = null;
        }
    }

    partial void OnDocumentNameChanged(string value)
    {
        ExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        ExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDocumentTypeChanged(ExportDocumentType value)
    {
        OnPropertyChanged(nameof(IsExcelSelected));
        OnPropertyChanged(nameof(IsMarkdownSelected));
        OnPropertyChanged(nameof(DocumentTypeHint));
        ExportCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task RetryLoadAsync()
    {
        UnsubscribeTree(ExportRootNode);
        ExportRootNode = null;
        NotifyExportTreeChanged();
        await InitializeAsync();
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (ExportRootNode == null)
        {
            return;
        }

        _isSynchronizingChecks = true;
        try
        {
            foreach (var node in EnumerateNodes(ExportRootNode))
            {
                if (node.ShowExportCheckBox)
                {
                    node.IsExportChecked = true;
                }
            }
        }
        finally
        {
            _isSynchronizingChecks = false;
        }

        RecalculateTreeCheckStates(ExportRootNode);
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (ExportRootNode == null)
        {
            return;
        }

        _isSynchronizingChecks = true;
        try
        {
            foreach (var node in EnumerateNodes(ExportRootNode))
            {
                if (node.ShowExportCheckBox)
                {
                    node.IsExportChecked = false;
                }
            }
        }
        finally
        {
            _isSynchronizingChecks = false;
        }

        RecalculateTreeCheckStates(ExportRootNode);
        NotifySelectionStateChanged();
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = null;
        OnCloseRequested();
    }

    [RelayCommand]
    private void SelectDocumentType(ExportDocumentType documentType)
    {
        SelectedDocumentType = documentType;
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        if (!ValidateBeforeClose())
        {
            return;
        }

        DialogResult = new ExportDialogResultDto
        {
            DocumentName = DocumentName.Trim(),
            OutputDirectory = OutputDirectory.Trim(),
            DocumentType = SelectedDocumentType,
            SelectedObjects = new Collection<ExportSelectedObjectDto>(
                EnumerateNodes(ExportRootNode)
                    .Where(node => node.IsExportableLeaf && node.IsExportChecked == true)
                    .Select(node => new ExportSelectedObjectDto
                    {
                        Name = node.Name,
                        SchemaName = ResolveSchemaName(node),
                        ObjectType = MapExportObjectType(node.NodeType)
                    })
                    .ToList())
        };

        OnCloseRequested();
    }

    private bool CanExport()
    {
        return !IsLoading && HasObjects;
    }

    private bool ValidateBeforeClose()
    {
        if (SelectedObjectCount == 0)
        {
            ToastService.ShowWarning("请至少选择一个导出对象。", 3000);
            return false;
        }

        if (string.IsNullOrWhiteSpace(DocumentName))
        {
            ToastService.ShowWarning("请填写文档名称。", 3000);
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            ToastService.ShowWarning("请选择输出目录。", 3000);
            return false;
        }

        try
        {
            Directory.CreateDirectory(OutputDirectory);
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"输出目录无效：{ex.Message}", 4000);
            return false;
        }

        if (!Enum.IsDefined(typeof(ExportDocumentType), SelectedDocumentType))
        {
            ToastService.ShowWarning("请选择有效的文档类型。", 3000);
            return false;
        }

        return true;
    }

    private async Task<TreeNodeItem> LoadExportTreeAsync()
    {
        if (_connection.DatabaseType == DatabaseType.MySql)
        {
            return await BuildMySqlTreeAsync(_connection);
        }

        var (success, rootNode, message) = await _databaseService.LoadDatabaseTreeAsync(_connection);
        if (!success || rootNode == null)
        {
            throw new InvalidOperationException(message);
        }

        PruneNonTableNodes(rootNode);
        return rootNode;
    }

    private async Task<TreeNodeItem> BuildMySqlTreeAsync(ConnectionConfig connection)
    {
        var schemaName = string.IsNullOrWhiteSpace(connection.Database)
            ? "default"
            : connection.Database;

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

        return rootNode;
    }

    private static void PruneNonTableNodes(TreeNodeItem rootNode)
    {
        var removableChildren = rootNode.Children
            .Where(child => child.NodeType == TreeNodeType.Folder &&
                            !string.Equals(child.Name, "Schemas", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var child in removableChildren)
        {
            rootNode.RemoveChild(child);
        }

        foreach (var schemaFolder in rootNode.Children.Where(child => child.NodeType == TreeNodeType.Folder))
        {
            foreach (var schemaNode in schemaFolder.Children.Where(child => child.NodeType == TreeNodeType.Schema))
            {
                var nonTableChildren = schemaNode.Children
                    .Where(child => child.NodeType != TreeNodeType.Table)
                    .ToList();

                foreach (var child in nonTableChildren)
                {
                    schemaNode.RemoveChild(child);
                }
            }
        }
    }

    private void PrepareExportTree(TreeNodeItem node)
    {
        node.ShowExportCheckBox = node.NodeType != TreeNodeType.Root;
        node.IsExportChecked = false;
        node.PropertyChanged += OnTreeNodePropertyChanged;

        foreach (var child in node.Children)
        {
            PrepareExportTree(child);
        }
    }

    private void UnsubscribeTree(TreeNodeItem? node)
    {
        if (node == null)
        {
            return;
        }

        node.PropertyChanged -= OnTreeNodePropertyChanged;
        foreach (var child in node.Children)
        {
            UnsubscribeTree(child);
        }
    }

    private void OnTreeNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TreeNodeItem.IsExportChecked) || _isSynchronizingChecks || sender is not TreeNodeItem node)
        {
            return;
        }

        _isSynchronizingChecks = true;
        try
        {
            if (node.IsExportChecked.HasValue)
            {
                foreach (var child in node.Children)
                {
                    SetCheckedDownward(child, node.IsExportChecked.Value);
                }
            }

            UpdateAncestors(node.Parent);
        }
        finally
        {
            _isSynchronizingChecks = false;
        }

        NotifySelectionStateChanged();
    }

    private void SetCheckedDownward(TreeNodeItem node, bool isChecked)
    {
        if (node.ShowExportCheckBox)
        {
            node.IsExportChecked = isChecked;
        }

        foreach (var child in node.Children)
        {
            SetCheckedDownward(child, isChecked);
        }
    }

    private void UpdateAncestors(TreeNodeItem? node)
    {
        while (node != null)
        {
            var childStates = node.Children
                .Where(child => child.ShowExportCheckBox)
                .Select(child => child.IsExportChecked)
                .ToList();

            if (childStates.Count > 0)
            {
                node.IsExportChecked = childStates.All(state => state == true)
                    ? true
                    : childStates.All(state => state == false)
                        ? false
                        : null;
            }

            node = node.Parent;
        }
    }

    private void RecalculateTreeCheckStates(TreeNodeItem rootNode)
    {
        foreach (var node in EnumerateNodes(rootNode).OrderByDescending(node => node.GetPath().Count))
        {
            if (node.Children.Count == 0)
            {
                continue;
            }

            var childStates = node.Children
                .Where(child => child.ShowExportCheckBox)
                .Select(child => child.IsExportChecked)
                .ToList();

            if (childStates.Count == 0)
            {
                continue;
            }

            node.IsExportChecked = childStates.All(state => state == true)
                ? true
                : childStates.All(state => state == false)
                    ? false
                    : null;
        }
    }

    private void NotifyExportTreeChanged()
    {
        OnPropertyChanged(nameof(ExportRootChildren));
        NotifySelectionStateChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(SelectedObjectCount));
        OnPropertyChanged(nameof(SelectionSummary));
        OnPropertyChanged(nameof(HasObjects));
        ExportCommand.NotifyCanExecuteChanged();
    }

    private static string ResolveInitialOutputDirectory(string? initialOutputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(initialOutputDirectory))
        {
            return initialOutputDirectory;
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return string.IsNullOrWhiteSpace(desktop)
            ? AppDomain.CurrentDomain.BaseDirectory
            : desktop;
    }

    private static IEnumerable<TreeNodeItem> EnumerateNodes(TreeNodeItem? node)
    {
        if (node == null)
        {
            yield break;
        }

        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static string ResolveSchemaName(TreeNodeItem node)
    {
        if (node.Data is TableModel table)
        {
            return table.Schema;
        }

        if (node.Data is AzrngTools.Models.Database.ViewModel view)
        {
            return view.Schema;
        }

        if (node.Data is StoredProcedureModel procedure)
        {
            return procedure.Schema;
        }

        var schemaNode = node.GetPath().LastOrDefault(pathNode => pathNode.NodeType == TreeNodeType.Schema);
        return schemaNode?.Name ?? string.Empty;
    }

    private static ExportObjectType MapExportObjectType(TreeNodeType nodeType)
    {
        return nodeType switch
        {
            TreeNodeType.Table => ExportObjectType.Table,
            _ => throw new InvalidOperationException($"Unsupported export node type: {nodeType}")
        };
    }

    public event EventHandler<object?>? RequestClose;

    public void Close()
    {
        RequestClose?.Invoke(this, null);
    }

    private void OnCloseRequested()
    {
        RequestClose?.Invoke(this, DialogResult);
    }
}
