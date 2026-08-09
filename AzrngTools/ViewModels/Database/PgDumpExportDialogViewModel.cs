using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azrng.Core.Model;
using Azrng.Core.Results;
using AzrngTools.Models.Database;
using AzrngTools.Models.Database.DTOs;
using AzrngTools.Services.Database;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace AzrngTools.ViewModels.Database;

/// <summary>
/// pg_dump 导出向导视图模型。流程：选库 → 选内容/格式 → 多选 schema → 指定 pg_dump.exe 与保存位置。
/// </summary>
public partial class PgDumpExportDialogViewModel : ViewModelBase, IDialogContext
{
    private readonly ConnectionConfig _connection;
    private readonly IDatabaseService _databaseService;
    private readonly string? _initialDatabaseName;
    private bool _initialized;
    private bool _isInitializing;
    private CancellationTokenSource? _schemaLoadCts;

    public PgDumpExportResultDto? DialogResult { get; private set; }

    public string ConnectionName => _connection.Name;

    public string ConnectionSummary => !string.IsNullOrWhiteSpace(SelectedDatabase)
        ? SelectedDatabase
        : _connection.Name;

    /// <summary>
    /// 设计时占位数据库服务，避免设计时真实连库。
    /// </summary>
    private static readonly IDatabaseService DesignTimeDatabaseService = new DatabaseService();

    public PgDumpExportDialogViewModel()
        : this(
            new ConnectionConfig
            {
                Name = "pgsql-demo",
                Host = "127.0.0.1",
                Port = 5432,
                Database = "chat",
                DatabaseType = DatabaseType.PostgresSql
            },
            "chat",
            DesignTimeDatabaseService,
            null)
    {
        AvailableDatabases = new ObservableCollection<string> { "chat", "postgres", "knowledgebase" };
        SelectedDatabase = "chat";
        Schemas = new ObservableCollection<PgDumpSchemaSelectionDto>
        {
            new() { Name = "public", IsDefault = true, TableCount = 12, IsSelected = true },
            new() { Name = "audit", TableCount = 3 },
            new() { Name = "reporting", TableCount = 5 }
        };
        PgDumpExePath = @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe";
    }

    public PgDumpExportDialogViewModel(
        ConnectionConfig connection,
        string? databaseName,
        IDatabaseService databaseService,
        string? lastExePath)
    {
        _connection = connection;
        _initialDatabaseName = databaseName;
        _databaseService = databaseService;
        PgDumpExePath = lastExePath ?? string.Empty;
    }

    [ObservableProperty]
    private ObservableCollection<string> _availableDatabases = new();

    [ObservableProperty]
    private string? _selectedDatabase;

    [ObservableProperty]
    private ObservableCollection<PgDumpSchemaSelectionDto> _schemas = new();

    [ObservableProperty]
    private ObservableCollection<PgDumpSchemaSelectionDto> _filteredSchemas = new();

    [ObservableProperty]
    private PgDumpContentMode _contentMode = PgDumpContentMode.SchemaAndData;

    [ObservableProperty]
    private PgDumpOutputFormat _outputFormat = PgDumpOutputFormat.Plain;

    [ObservableProperty]
    private string _pgDumpExePath = string.Empty;

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    private string _schemaSearchText = string.Empty;

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private string _loadErrorMessage = string.Empty;

    public int SelectedSchemaCount => Schemas.Count(s => s.IsSelected);

    public bool HasSelection => SelectedSchemaCount > 0;

    public string SelectionSummary => SelectedSchemaCount == 0
        ? "请选择 schema"
        : $"已选 {SelectedSchemaCount} / {Schemas.Count} 个 schema";

    public string SuggestedOutputFileName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(SelectedDatabase) ? ConnectionName : SelectedDatabase;
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var ext = OutputFormat == PgDumpOutputFormat.Custom ? "dump" : "sql";
            return $"{name}_{stamp}.{ext}";
        }
    }

    public bool IsSchemaOnlyMode
    {
        get => ContentMode == PgDumpContentMode.SchemaOnly;
        set { if (value) ContentMode = PgDumpContentMode.SchemaOnly; }
    }

    public bool IsDataOnlyMode
    {
        get => ContentMode == PgDumpContentMode.DataOnly;
        set { if (value) ContentMode = PgDumpContentMode.DataOnly; }
    }

    public bool IsSchemaAndDataMode
    {
        get => ContentMode == PgDumpContentMode.SchemaAndData;
        set { if (value) ContentMode = PgDumpContentMode.SchemaAndData; }
    }

    public bool IsPlainFormat
    {
        get => OutputFormat == PgDumpOutputFormat.Plain;
        set { if (value) OutputFormat = PgDumpOutputFormat.Plain; }
    }

    public bool IsCustomFormat
    {
        get => OutputFormat == PgDumpOutputFormat.Custom;
        set { if (value) OutputFormat = PgDumpOutputFormat.Custom; }
    }

    partial void OnSelectedDatabaseChanged(string? value)
    {
        ExportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ConnectionSummary));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
        // _isInitializing 守卫：InitializeAsync 内会显式 await 加载，避免重复触发；
        // 仅用户手动切库时 fire，并用 CancellationToken 取消上一个请求，防止旧结果覆盖新结果
        if (_initialized && !_isInitializing && !string.IsNullOrWhiteSpace(value))
        {
            _schemaLoadCts?.Cancel();
            _schemaLoadCts = new CancellationTokenSource();
            _ = LoadSchemasAsync(value!, _schemaLoadCts.Token);
        }
    }

    partial void OnContentModeChanged(PgDumpContentMode value)
    {
        OnPropertyChanged(nameof(IsSchemaOnlyMode));
        OnPropertyChanged(nameof(IsDataOnlyMode));
        OnPropertyChanged(nameof(IsSchemaAndDataMode));
    }

    partial void OnOutputFormatChanged(PgDumpOutputFormat value)
    {
        OnPropertyChanged(nameof(IsPlainFormat));
        OnPropertyChanged(nameof(IsCustomFormat));
        OnPropertyChanged(nameof(SuggestedOutputFileName));
    }

    partial void OnSchemasChanged(ObservableCollection<PgDumpSchemaSelectionDto> value)
    {
        foreach (var item in value)
        {
            item.PropertyChanged -= OnSchemaItemChanged;
            item.PropertyChanged += OnSchemaItemChanged;
        }

        RebuildFilteredSchemas();
        RefreshSelection();
    }

    partial void OnSchemaSearchTextChanged(string value)
    {
        RebuildFilteredSchemas();
    }

    partial void OnPgDumpExePathChanged(string value) => ExportCommand.NotifyCanExecuteChanged();

    partial void OnOutputFilePathChanged(string value) => ExportCommand.NotifyCanExecuteChanged();

    private void OnSchemaItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PgDumpSchemaSelectionDto.IsSelected))
        {
            RefreshSelection();
        }
    }

    private void RebuildFilteredSchemas()
    {
        var keyword = (SchemaSearchText ?? string.Empty).Trim();
        var visible = string.IsNullOrEmpty(keyword)
            ? Schemas.ToList()
            : Schemas.Where(s => s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        FilteredSchemas = new ObservableCollection<PgDumpSchemaSelectionDto>(visible);
    }

    private void RefreshSelection()
    {
        OnPropertyChanged(nameof(SelectedSchemaCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionSummary));
        ExportCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeAsync()
    {
        if (_initialized || IsLoading)
        {
            return;
        }

        _initialized = true;
        _isInitializing = true;
        IsLoading = true;
        LoadingText = "正在加载数据库列表...";
        HasLoadError = false;
        LoadErrorMessage = string.Empty;

        try
        {
            var dbResult = await _databaseService.GetDatabaseNamesAsync(_connection);
            if (!dbResult.IsSuccess)
            {
                throw new InvalidOperationException(dbResult.Message);
            }

            var names = dbResult.DataOrEmpty().OrderBy(n => n).ToList();
            AvailableDatabases = new ObservableCollection<string>(names);

            var target = !string.IsNullOrWhiteSpace(_initialDatabaseName) && names.Contains(_initialDatabaseName!)
                ? _initialDatabaseName
                : names.FirstOrDefault();
            SelectedDatabase = target;

            // 显式 await 加载 schema，避免依赖 OnSelectedDatabaseChanged 的 fire-and-forget
            // 与本方法 finally 产生 IsLoading 提前消失的竞态
            if (!string.IsNullOrWhiteSpace(target))
            {
                await LoadSchemasAsync(target!, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            HasLoadError = true;
            LoadErrorMessage = ex.Message;
            LoggingService.LogError("Failed to initialize pg_dump export dialog.", ex);
            ToastService.ShowError($"加载数据库列表失败：{ex.Message}", 6000);
        }
        finally
        {
            _isInitializing = false;
            IsLoading = false;
            LoadingText = null;
        }
    }

    private async Task LoadSchemasAsync(string database, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsLoading = true;
        LoadingText = $"正在加载 {database} 的 schema...";
        try
        {
            var conn = CloneWithDatabase(_connection, database);
            var schemaResult = await _databaseService.GetSchemasAsync(conn);
            // 切库时旧请求可能已被取消，丢弃其结果避免覆盖新库的 schema
            cancellationToken.ThrowIfCancellationRequested();
            if (!schemaResult.IsSuccess)
            {
                throw new InvalidOperationException(schemaResult.Message);
            }

            Schemas = new ObservableCollection<PgDumpSchemaSelectionDto>(
                schemaResult.DataOrEmpty()
                    .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new PgDumpSchemaSelectionDto
                    {
                        Name = s.Name,
                        Owner = s.Owner,
                        TableCount = s.TableCount,
                        IsDefault = s.IsDefault
                    }));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            HasLoadError = true;
            LoadErrorMessage = ex.Message;
            LoggingService.LogError($"Failed to load schemas for {database}.", ex);
            ToastService.ShowError($"加载 schema 失败：{ex.Message}", 6000);
            Schemas = new ObservableCollection<PgDumpSchemaSelectionDto>();
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
                LoadingText = null;
            }
        }
    }

    [RelayCommand]
    private async Task RetryLoadAsync()
    {
        _initialized = false;
        await InitializeAsync();
    }

    [RelayCommand]
    private void SelectAllSchemas()
    {
        foreach (var s in Schemas)
        {
            s.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSchemaSelection()
    {
        foreach (var s in Schemas)
        {
            s.IsSelected = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = null;
        OnCloseRequested();
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Export()
    {
        if (!ValidateBeforeClose())
        {
            return;
        }

        DialogResult = new PgDumpExportResultDto
        {
            Database = SelectedDatabase!,
            ContentMode = ContentMode,
            OutputFormat = OutputFormat,
            SelectedSchemas = new Collection<string>(
                Schemas.Where(s => s.IsSelected).Select(s => s.Name).ToList()),
            PgDumpExePath = PgDumpExePath,
            OutputFilePath = OutputFilePath
        };

        OnCloseRequested();
    }

    private bool CanExport()
    {
        return !IsLoading
            && !string.IsNullOrWhiteSpace(SelectedDatabase)
            && HasSelection
            && !string.IsNullOrWhiteSpace(PgDumpExePath)
            && File.Exists(PgDumpExePath)
            && !string.IsNullOrWhiteSpace(OutputFilePath);
    }

    private bool ValidateBeforeClose()
    {
        if (string.IsNullOrWhiteSpace(SelectedDatabase))
        {
            ToastService.ShowWarning("请选择要导出的数据库。", 3000);
            return false;
        }

        if (SelectedSchemaCount == 0)
        {
            ToastService.ShowWarning("请至少选择一个 schema。", 3000);
            return false;
        }

        if (string.IsNullOrWhiteSpace(PgDumpExePath))
        {
            ToastService.ShowWarning("请选择 pg_dump 可执行文件。", 3000);
            return false;
        }

        if (!File.Exists(PgDumpExePath))
        {
            ToastService.ShowError($"找不到 pg_dump 文件：{PgDumpExePath}", 4000);
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputFilePath))
        {
            ToastService.ShowWarning("请选择导出文件的保存位置。", 3000);
            return false;
        }

        return true;
    }

    private static ConnectionConfig CloneWithDatabase(ConnectionConfig source, string database)
    {
        return new ConnectionConfig
        {
            Name = source.Name,
            DatabaseType = source.DatabaseType,
            Host = source.Host,
            Port = source.Port,
            Username = source.Username,
            Password = source.Password,
            Database = database
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
