using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Azrng.Core.Results;
using AzrngTools.Models.Pdf;
using AzrngTools.Services;
using AzrngTools.Services.Pdf;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Pdf;

public partial class PdfManagementPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "pdf-management";

    private readonly IMessageService _messageService;
    private readonly IPdfProcessingService _pdfProcessingService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public PdfManagementPageViewModel(
        IPdfProcessingService pdfProcessingService,
        IMessageService messageService,
        IPageInputStoreService pageInputStore)
    {
        _pdfProcessingService = pdfProcessingService;
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        // 构造初始化赋值不属于用户输入，置位跳过保存提交，避免默认值快照在防抖落盘时覆盖已有存档
        _isRestoring = true;
        PageRangeText = "1";
        _isRestoring = false;
        StatusText = "请选择 PDF 文件";
        RestoreInputs();
    }

    [ObservableProperty]
    private PdfLoadedFileSummary? _currentFile;

    [ObservableProperty]
    private string _pageRangeText = string.Empty;

    [ObservableProperty]
    private bool _isIndividualPagesMode;

    [ObservableProperty]
    private string _splitOutputPath = string.Empty;

    [ObservableProperty]
    private string _wordOutputPath = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _lastResultText = string.Empty;

    public bool HasFile => CurrentFile is not null;

    public bool HasLastResult => !string.IsNullOrWhiteSpace(LastResultText);

    public bool CanOperate => HasFile && !IsBusy;

    /// <summary>
    /// 空闲态可执行(选择文件与清空不依赖已加载文件),供命令 CanExecute 绑定驱动按钮禁用
    /// </summary>
    private bool CanExecuteWhileIdle() => !IsBusy;

    public string FileName => CurrentFile?.FileName ?? "未选择 PDF";

    public string FilePath => CurrentFile?.FilePath ?? string.Empty;

    public string FileSizeText => CurrentFile is null ? "-" : FormatFileSize(CurrentFile.FileSizeBytes);

    public string PageCountText => CurrentFile is null ? "-" : $"{CurrentFile.PageCount} 页";

    public string SelectedPageSummary
    {
        get
        {
            if (CurrentFile is null)
            {
                return "未选择文件";
            }

            var selection = PdfPageRangeParser.Parse(PageRangeText, CurrentFile.PageCount);
            return selection.IsSuccess
                ? $"已选择 {selection.Count} 页"
                : selection.ErrorMessage ?? "页码格式不正确";
        }
    }

    public string SplitModeText => IsIndividualPagesMode ? "逐页导出" : "合并为单个 PDF";

    [RelayCommand(CanExecute = nameof(CanExecuteWhileIdle))]
    private async Task ChoosePdfFile()
    {
        if (IsBusy)
        {
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            _messageService.SendMessage("无法打开文件选择器");
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 PDF 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("PDF 文件") { Patterns = ["*.pdf"] },
                FilePickerFileTypes.All
            ]
        });

        var filePath = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await LoadPdfAsync(filePath);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ChooseSplitOutput()
    {
        if (!CanOperate)
        {
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            _messageService.SendMessage("无法打开输出路径选择器");
            return;
        }

        if (IsIndividualPagesMode)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择逐页导出目录",
                AllowMultiple = false
            });

            SplitOutputPath = folders.FirstOrDefault()?.Path.LocalPath ?? SplitOutputPath;
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存分割后的 PDF",
            DefaultExtension = "pdf",
            SuggestedFileName = BuildSuggestedPdfFileName(),
            FileTypeChoices =
            [
                new FilePickerFileType("PDF 文件") { Patterns = ["*.pdf"] },
                FilePickerFileTypes.All
            ]
        });

        SplitOutputPath = file?.Path.LocalPath ?? SplitOutputPath;
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ChooseWordOutput()
    {
        if (!CanOperate)
        {
            return;
        }

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            _messageService.SendMessage("无法打开输出路径选择器");
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出 Word 文件",
            DefaultExtension = "docx",
            SuggestedFileName = BuildSuggestedWordFileName(),
            FileTypeChoices =
            [
                new FilePickerFileType("Word 文档") { Patterns = ["*.docx"] },
                FilePickerFileTypes.All
            ]
        });

        WordOutputPath = file?.Path.LocalPath ?? WordOutputPath;
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task SplitPdf()
    {
        if (!CanOperate || CurrentFile is null)
        {
            return;
        }

        var selection = PdfPageRangeParser.Parse(PageRangeText, CurrentFile.PageCount);
        if (!selection.IsSuccess)
        {
            ShowFailure(selection.ErrorMessage ?? "页码格式不正确");
            return;
        }

        if (string.IsNullOrWhiteSpace(SplitOutputPath))
        {
            ShowFailure(IsIndividualPagesMode ? "请选择分割输出目录" : "请选择分割输出文件");
            return;
        }

        await RunBusyAsync("正在分割 PDF...", async () =>
        {
            var request = new PdfSplitRequest(
                CurrentFile.FilePath,
                selection.Pages,
                IsIndividualPagesMode ? PdfSplitMode.IndividualPages : PdfSplitMode.SingleFile,
                SplitOutputPath);

            var result = await _pdfProcessingService.SplitAsync(request);
            HandleResult(result, value =>
            {
                return IsIndividualPagesMode
                    ? $"PDF 分割完成，共导出 {value.OutputFileCount} 个文件"
                    : $"PDF 分割完成：{value.OutputFiles.FirstOrDefault()}";
            });
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ExportWord()
    {
        if (!CanOperate || CurrentFile is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(WordOutputPath))
        {
            ShowFailure("请选择 Word 输出文件");
            return;
        }

        await RunBusyAsync("正在导出 Word...", async () =>
        {
            var result = await _pdfProcessingService.ExportWordAsync(new PdfWordExportRequest(
                CurrentFile.FilePath,
                WordOutputPath));
            HandleResult(result, value => $"Word 导出完成：{value.OutputFilePath}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhileIdle))]
    private void Clear()
    {
        if (IsBusy)
        {
            return;
        }

        CurrentFile = null;
        PageRangeText = "1";
        SplitOutputPath = string.Empty;
        WordOutputPath = string.Empty;
        LastResultText = string.Empty;
        StatusText = "请选择 PDF 文件";
    }

    internal async Task LoadPdfAsync(string filePath)
    {
        await RunBusyAsync("正在加载 PDF...", async () =>
        {
            var result = await _pdfProcessingService.LoadMetadataAsync(filePath);
            if (!result.IsSuccess)
            {
                ShowFailure(result.Message ?? "PDF 加载失败");
                CurrentFile = null;
                return;
            }

            if (result.Data is null)
            {
                ShowFailure("PDF 加载失败：未读取到文件信息");
                CurrentFile = null;
                return;
            }

            var loadedFile = result.Data;
            CurrentFile = loadedFile;
            PageRangeText = loadedFile.PageCount <= 0 ? string.Empty : $"1-{loadedFile.PageCount}";
            SplitOutputPath = BuildDefaultSplitOutputPath(loadedFile.FilePath);
            WordOutputPath = BuildDefaultWordOutputPath(loadedFile.FilePath);
            LastResultText = string.Empty;
            StatusText = result.Message ?? "PDF 加载完成";
            _messageService.SendMessage(StatusText);
        });
    }

    private async Task RunBusyAsync(string busyText, Func<Task> action)
    {
        IsBusy = true;
        BusyText = busyText;
        StatusText = busyText;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            AppLog.LogError($"{busyText}失败: {ex.GetExceptionAndStack()}");
            ShowFailure($"{busyText}失败：{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    private void HandleResult<T>(IResultModel<T> result, Func<T, string> successMessageFactory)
    {
        if (!result.IsSuccess || result.Data is null)
        {
            ShowFailure(result.Message ?? "操作失败");
            return;
        }

        var message = successMessageFactory(result.Data);
        LastResultText = message;
        StatusText = message;
        _messageService.SendMessage(message);
    }

    private void ShowFailure(string message)
    {
        LastResultText = message;
        StatusText = message;
        _messageService.SendMessage(message, "PDF 管理");
    }

    partial void OnCurrentFileChanged(PdfLoadedFileSummary? value)
    {
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(CanOperate));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileSizeText));
        OnPropertyChanged(nameof(PageCountText));
        OnPropertyChanged(nameof(SelectedPageSummary));
        NotifyCommandStates();
    }

    partial void OnPageRangeTextChanged(string value)
    {
        // 恢复期间保留页码摘要通知，仅跳过保存提交避免恢复即回写
        if (!_isRestoring)
        {
            SaveInputs();
        }

        OnPropertyChanged(nameof(SelectedPageSummary));
    }

    partial void OnIsIndividualPagesModeChanged(bool value)
    {
        // 恢复期间保留分割模式联动（此时 CurrentFile 尚为空，联动自动跳过），仅跳过保存提交
        if (!_isRestoring)
        {
            SaveInputs();
        }

        OnPropertyChanged(nameof(SplitModeText));
        if (CurrentFile is null)
        {
            return;
        }

        SplitOutputPath = value
            ? Path.GetDirectoryName(CurrentFile.FilePath) ?? string.Empty
            : BuildDefaultSplitOutputPath(CurrentFile.FilePath);
    }

    partial void OnSplitOutputPathChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnWordOutputPathChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    // 只保存页码与输出路径配置；当前文件（CurrentFile）不入存档
    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            [nameof(PageRangeText)] = PageInputValues.Encode(PageRangeText),
            [nameof(IsIndividualPagesMode)] = PageInputValues.Encode(IsIndividualPagesMode),
            [nameof(SplitOutputPath)] = PageInputValues.Encode(SplitOutputPath),
            [nameof(WordOutputPath)] = PageInputValues.Encode(WordOutputPath),
        });
    }

    private void RestoreInputs()
    {
        var saved = _pageInputStore.TryLoad(PageId);
        if (saved.Count == 0)
        {
            return;
        }

        _isRestoring = true;
        try
        {
            if (saved.TryGetValue(nameof(PageRangeText), out var pageRangeText) && pageRangeText is not null)
            {
                PageRangeText = pageRangeText;
            }

            if (saved.TryGetValue(nameof(IsIndividualPagesMode), out var isIndividualPagesMode))
            {
                IsIndividualPagesMode = PageInputValues.DecodeBool(isIndividualPagesMode, IsIndividualPagesMode);
            }

            if (saved.TryGetValue(nameof(SplitOutputPath), out var splitOutputPath) && splitOutputPath is not null)
            {
                SplitOutputPath = splitOutputPath;
            }

            if (saved.TryGetValue(nameof(WordOutputPath), out var wordOutputPath) && wordOutputPath is not null)
            {
                WordOutputPath = wordOutputPath;
            }
        }
        finally
        {
            _isRestoring = false;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOperate));
        NotifyCommandStates();
    }

    partial void OnLastResultTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasLastResult));
    }

    private void NotifyCommandStates()
    {
        ChoosePdfFileCommand.NotifyCanExecuteChanged();
        ChooseSplitOutputCommand.NotifyCanExecuteChanged();
        ChooseWordOutputCommand.NotifyCanExecuteChanged();
        SplitPdfCommand.NotifyCanExecuteChanged();
        ExportWordCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private string BuildSuggestedPdfFileName()
    {
        return CurrentFile is null
            ? "split.pdf"
            : $"{Path.GetFileNameWithoutExtension(CurrentFile.FileName)}_split.pdf";
    }

    private string BuildSuggestedWordFileName()
    {
        return CurrentFile is null
            ? "export.docx"
            : $"{Path.GetFileNameWithoutExtension(CurrentFile.FileName)}.docx";
    }

    private static string BuildDefaultSplitOutputPath(string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var fileName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_split.pdf";
        return Path.Combine(directory, fileName);
    }

    private static string BuildDefaultWordOutputPath(string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var fileName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.docx";
        return Path.Combine(directory, fileName);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:F1} KB";
        }

        return $"{bytes / 1024d / 1024d:F1} MB";
    }

    private static TopLevel? GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
