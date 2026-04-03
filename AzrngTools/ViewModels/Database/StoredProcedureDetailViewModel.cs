using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AzrngTools.Models.Database;
using AzrngTools.Services.Database;

namespace AzrngTools.ViewModels.Database;

public partial class StoredProcedureDetailViewModel : ViewModelBase
{
    private readonly DatabaseService _databaseService = new();

    public bool HasSelectedProcedure => SelectedProcedure != null;
    public bool HasProcedureDefinition => !string.IsNullOrWhiteSpace(ProcedureDefinition);
    public string ProcedureDefinitionDisplay => HasProcedureDefinition
        ? ProcedureDefinition!
        : "-- 暂无 DDL 定义";

    [ObservableProperty]
    private StoredProcedureModel? _selectedProcedure;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadingText;

    [ObservableProperty]
    private string? _procedureComment;

    [ObservableProperty]
    private string? _schemaName;

    [ObservableProperty]
    private DateTime? _createTime;

    [ObservableProperty]
    private DateTime? _modifyTime;

    [ObservableProperty]
    private string? _procedureDefinition;

    [ObservableProperty]
    private string? _parameters;

    [ObservableProperty]
    private ConnectionConfig? _currentConnection;

    [ObservableProperty]
    private ObservableCollection<StoredProcedureModel> _procedures = new();

    [ObservableProperty]
    private bool _showObjectList = true;

    public bool ShowDetailOnly => !ShowObjectList;

    public bool ShowEmptyState => ShowObjectList && Procedures.Count == 0;

    public bool ShowSplitWorkspace => ShowObjectList && Procedures.Count > 0;

    public StoredProcedureDetailViewModel()
    {
        SubscribeToProceduresCollection(Procedures);
    }

    public async Task LoadProceduresBySchemaAsync(string schemaName)
    {
        LoggingService.LogInfo($"LoadProceduresBySchemaAsync 开始: schema={schemaName}, connection={CurrentConnection?.Name ?? "null"}");

        if (CurrentConnection == null || string.IsNullOrWhiteSpace(schemaName))
        {
            LoggingService.LogWarning("LoadProceduresBySchemaAsync: 未选择连接或架构名称为空");
            System.Diagnostics.Debug.WriteLine("未选择连接或架构名称为空");
            return;
        }

        IsLoading = true;
        LoadingText = $"正在加载架构 [{schemaName}] 下的存储过程...";
        Procedures.Clear();
        SelectedProcedure = null;

        try
        {
            LoggingService.LogInfo($"开始调用 DatabaseService.GetStoredProceduresAsync: {CurrentConnection.Name}, schema={schemaName}");
            var result = await _databaseService.GetStoredProceduresAsync(CurrentConnection, schemaName);

            if (result.Success)
            {
                foreach (var procedure in result.Procedures)
                {
                    Procedures.Add(procedure);
                }

                LoadingText = $"已加载 {Procedures.Count} 个存储过程。";
                LoggingService.LogInfo($"加载存储过程列表成功: {Procedures.Count} 条");
                System.Diagnostics.Debug.WriteLine($"加载存储过程列表: {Procedures.Count} 条");
            }
            else
            {
                LoadingText = $"加载存储过程失败: {result.Message}";
                LoggingService.LogError($"加载存储过程列表失败: {result.Message}");
                System.Diagnostics.Debug.WriteLine($"加载存储过程列表失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            LoadingText = $"加载存储过程异常: {ex.Message}";
            LoggingService.LogError("加载存储过程列表异常", ex);
            System.Diagnostics.Debug.WriteLine($"加载存储过程列表异常: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (SelectedProcedure == null || CurrentConnection == null)
        {
            System.Diagnostics.Debug.WriteLine("未选择存储过程或连接配置");
            return;
        }

        IsLoading = true;
        LoadingText = $"正在加载存储过程 {SelectedProcedure.Name} 的详情...";

        try
        {
            ProcedureComment = SelectedProcedure.Comment;
            SchemaName = SelectedProcedure.Schema;
            ProcedureDefinition = SelectedProcedure.Definition;
            Parameters = SelectedProcedure.Parameters;
            CreateTime = SelectedProcedure.CreateTime;
            ModifyTime = SelectedProcedure.ModifyTime;

            if (string.IsNullOrWhiteSpace(ProcedureDefinition))
            {
                var (success, definition, _) = await _databaseService.GetStoredProcedureDefinitionAsync(
                    CurrentConnection,
                    SelectedProcedure.Schema ?? "dbo",
                    SelectedProcedure.Name);

                if (success)
                {
                    ProcedureDefinition = definition;
                }
            }

            OnPropertyChanged(nameof(HasProcedureDefinition));
            OnPropertyChanged(nameof(ProcedureDefinitionDisplay));
            LoadingText = $"已加载存储过程 {SelectedProcedure.Name} 的 DDL。";
        }
        catch (Exception ex)
        {
            LoadingText = $"加载存储过程详情失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"加载存储过程详情失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedProcedure != null)
        {
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private void Clear()
    {
        SelectedProcedure = null;
        ProcedureComment = null;
        SchemaName = null;
        ProcedureDefinition = null;
        Parameters = null;
        CreateTime = null;
        ModifyTime = null;
        SelectedTabIndex = 0;
        OnPropertyChanged(nameof(HasProcedureDefinition));
        OnPropertyChanged(nameof(ProcedureDefinitionDisplay));
    }

    partial void OnSelectedProcedureChanged(StoredProcedureModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedProcedure));

        if (value != null)
        {
            System.Diagnostics.Debug.WriteLine($"选中存储过程: {value.Name}");
            _ = LoadDataAsync();
        }
    }

    partial void OnShowObjectListChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDetailOnly));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    partial void OnProceduresChanged(ObservableCollection<StoredProcedureModel> value)
    {
        SubscribeToProceduresCollection(value);
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    private void SubscribeToProceduresCollection(ObservableCollection<StoredProcedureModel> procedures)
    {
        procedures.CollectionChanged += OnProceduresCollectionChanged;
    }

    private void OnProceduresCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(ShowSplitWorkspace));
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        System.Diagnostics.Debug.WriteLine($"切换到存储过程标签页: {value}");
    }

    partial void OnProcedureDefinitionChanged(string? value)
    {
        OnPropertyChanged(nameof(HasProcedureDefinition));
        OnPropertyChanged(nameof(ProcedureDefinitionDisplay));
    }
}
