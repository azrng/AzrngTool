using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartSQL.UI.Models;
using SmartSQL.UI.Services;

namespace SmartSQL.UI.ViewModels;

public partial class ViewDetailViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService = new();

    public bool HasSelectedView => SelectedView != null;
    public bool HasViewDefinition => !string.IsNullOrWhiteSpace(ViewDefinition);
    public string ViewDefinitionDisplay => HasViewDefinition
        ? ViewDefinition!
        : "-- 暂无 DDL 定义";

    [ObservableProperty]
    private ViewModel? _selectedView;

    [ObservableProperty]
    private ObservableCollection<ColumnModel> _columns = new();

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadingText;

    [ObservableProperty]
    private string? _viewComment;

    [ObservableProperty]
    private string? _schemaName;

    [ObservableProperty]
    private DateTime? _createTime;

    [ObservableProperty]
    private DateTime? _modifyTime;

    [ObservableProperty]
    private string? _viewDefinition;

    [ObservableProperty]
    private ConnectionConfig? _currentConnection;

    [ObservableProperty]
    private ObservableCollection<ViewModel> _views = new();

    [ObservableProperty]
    private bool _showObjectList = true;

    public bool ShowDetailOnly => !ShowObjectList;

    public bool ShowEmptyState => ShowObjectList && Views.Count == 0;

    public bool ShowSplitWorkspace => ShowObjectList && Views.Count > 0;

    public ViewDetailViewModel()
    {
        SubscribeToViewsCollection(Views);
    }

    public async Task LoadViewsBySchemaAsync(string schemaName)
    {
        LoggingService.LogInfo($"LoadViewsBySchemaAsync 开始: schema={schemaName}, connection={CurrentConnection?.Name ?? "null"}");

        if (CurrentConnection == null || string.IsNullOrWhiteSpace(schemaName))
        {
            LoggingService.LogWarning("LoadViewsBySchemaAsync: 未选择连接或架构名称为空");
            System.Diagnostics.Debug.WriteLine("未选择连接或架构名称为空");
            return;
        }

        IsLoading = true;
        LoadingText = $"正在加载架构 [{schemaName}] 下的视图...";
        Views.Clear();
        SelectedView = null;

        try
        {
            LoggingService.LogInfo($"开始调用 DatabaseService.GetViewsAsync: {CurrentConnection.Name}, schema={schemaName}");
            var result = await _databaseService.GetViewsAsync(CurrentConnection, schemaName);

            if (result.Success)
            {
                foreach (var view in result.Views)
                {
                    Views.Add(view);
                }

                LoadingText = $"已加载 {Views.Count} 个视图。";
                LoggingService.LogInfo($"加载视图列表成功: {Views.Count} 条");
                System.Diagnostics.Debug.WriteLine($"加载视图列表: {Views.Count} 条");
            }
            else
            {
                LoadingText = $"加载视图失败: {result.Message}";
                LoggingService.LogError($"加载视图列表失败: {result.Message}");
                System.Diagnostics.Debug.WriteLine($"加载视图列表失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            LoadingText = $"加载视图异常: {ex.Message}";
            LoggingService.LogError("加载视图列表异常", ex);
            System.Diagnostics.Debug.WriteLine($"加载视图列表异常: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (SelectedView == null || CurrentConnection == null)
        {
            System.Diagnostics.Debug.WriteLine("未选择视图或连接配置");
            return;
        }

        IsLoading = true;
        LoadingText = $"正在加载视图 {SelectedView.Name} 的详情...";
        Columns.Clear();

        try
        {
            ViewComment = SelectedView.Comment;
            SchemaName = SelectedView.Schema;
            ViewDefinition = SelectedView.Definition;
            CreateTime = SelectedView.CreateTime;
            ModifyTime = SelectedView.ModifyTime;

            if (string.IsNullOrWhiteSpace(ViewDefinition))
            {
                var (success, definition, _) = await _databaseService.GetViewDefinitionAsync(
                    CurrentConnection,
                    SelectedView.Schema ?? "dbo",
                    SelectedView.Name);

                if (success)
                {
                    ViewDefinition = definition;
                }
            }

            OnPropertyChanged(nameof(HasViewDefinition));
            OnPropertyChanged(nameof(ViewDefinitionDisplay));
            LoadingText = $"已加载视图 {SelectedView.Name} 的 DDL。";
        }
        catch (Exception ex)
        {
            LoadingText = $"加载视图详情失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"加载视图详情失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedView != null)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedView = null;
        Columns.Clear();
        ViewComment = null;
        SchemaName = null;
        ViewDefinition = null;
        CreateTime = null;
        ModifyTime = null;
        SelectedTabIndex = 0;
        OnPropertyChanged(nameof(HasViewDefinition));
        OnPropertyChanged(nameof(ViewDefinitionDisplay));
    }

    partial void OnSelectedViewChanged(ViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedView));

        if (value != null)
        {
            System.Diagnostics.Debug.WriteLine($"选中视图: {value.Name}");
            _ = LoadDataAsync();
        }
    }

    partial void OnShowObjectListChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetailOnly));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    partial void OnViewsChanged(ObservableCollection<ViewModel> value)
    {
        SubscribeToViewsCollection(value);
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    private void SubscribeToViewsCollection(ObservableCollection<ViewModel> views)
    {
        views.CollectionChanged += OnViewsCollectionChanged;
    }

    private void OnViewsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        System.Diagnostics.Debug.WriteLine($"切换到视图标签页: {value}");
    }

    partial void OnViewDefinitionChanged(string? value)
    {
        OnPropertyChanged(nameof(HasViewDefinition));
        OnPropertyChanged(nameof(ViewDefinitionDisplay));
    }
}
