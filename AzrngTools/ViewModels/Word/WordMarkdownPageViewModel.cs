using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Azrng.Core.Results;
using AzrngTools.Models.Word;
using AzrngTools.Services;
using AzrngTools.Services.Word;
using AzrngTools.Utils.Events;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Word;

public partial class WordMarkdownPageViewModel : ViewModelBase
{
    /// <summary>输入记忆存档的页面标识（DES-SETTINGS-002 接入清单）。</summary>
    private const string PageId = "word-to-markdown";

    private readonly IMessageService _messageService;
    private readonly IWordToMarkdownService _wordToMarkdownService;
    private readonly IPageInputStoreService _pageInputStore;

    // 构造恢复期间的属性赋值会再次触发 OnXxxChanged，置位跳过保存提交避免恢复即回写
    private bool _isRestoring;

    public WordMarkdownPageViewModel(
        IWordToMarkdownService wordToMarkdownService,
        IMessageService messageService,
        IPageInputStoreService pageInputStore)
    {
        _wordToMarkdownService = wordToMarkdownService;
        _messageService = messageService;
        _pageInputStore = pageInputStore;
        // 构造初始化赋值不属于用户输入，置位跳过保存提交，避免默认值快照在防抖落盘时覆盖已有存档
        _isRestoring = true;
        ExportImages = true;
        _isRestoring = false;
        StatusText = "请选择 Word 文件";
        RestoreInputs();
    }

    [ObservableProperty]
    private WordLoadedFileSummary? _currentFile;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    /// <summary>是否导出图片到 md 同级 .assets 目录（关闭则忽略全部图片）。</summary>
    [ObservableProperty]
    private bool _exportImages;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _lastResultText = string.Empty;

    /// <summary>转换成功后的 Markdown 源文本，供预览区展示。</summary>
    [ObservableProperty]
    private string _previewText = string.Empty;

    public bool HasFile => CurrentFile is not null;

    public bool HasLastResult => !string.IsNullOrWhiteSpace(LastResultText);

    public bool HasPreview => !string.IsNullOrWhiteSpace(PreviewText);

    public bool CanOperate => HasFile && !IsBusy;

    /// <summary>
    /// 空闲态可执行(选择文件与清空不依赖已加载文件),供命令 CanExecute 绑定驱动按钮禁用
    /// </summary>
    private bool CanExecuteWhileIdle() => !IsBusy;

    public string FileName => CurrentFile?.FileName ?? "未选择 Word";

    public string FilePath => CurrentFile?.FilePath ?? string.Empty;

    public string FileSizeText => CurrentFile is null ? "-" : FormatFileSize(CurrentFile.FileSizeBytes);

    public string ImageModeText => ExportImages ? "图片导出到 .assets 目录" : "忽略图片";

    [RelayCommand(CanExecute = nameof(CanExecuteWhileIdle))]
    private async Task ChooseWordFile()
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
            Title = "选择 Word 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Word 文档") { Patterns = ["*.docx"] },
                FilePickerFileTypes.All
            ]
        });

        var filePath = files.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await LoadWordAsync(filePath);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ChooseOutput()
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
            Title = "导出 Markdown 文件",
            DefaultExtension = "md",
            SuggestedFileName = BuildSuggestedFileName(),
            FileTypeChoices =
            [
                new FilePickerFileType("Markdown 文件") { Patterns = ["*.md"] },
                FilePickerFileTypes.All
            ]
        });

        OutputPath = file?.Path.LocalPath ?? OutputPath;
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task Convert()
    {
        if (!CanOperate || CurrentFile is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            ShowFailure("请选择 Markdown 输出文件");
            return;
        }

        await RunBusyAsync("正在转换为 Markdown...", async () =>
        {
            var result = await _wordToMarkdownService.ConvertAsync(new WordMarkdownConvertRequest(
                CurrentFile.FilePath,
                OutputPath,
                ExportImages));
            HandleResult(result, value =>
            {
                PreviewText = value.Markdown;
                var imageSummary = value.ExportedImageCount > 0
                    ? $"，导出图片 {value.ExportedImageCount} 张至 {value.AssetsDirectoryName}"
                    : "，未导出图片";
                return $"Markdown 转换完成：{value.OutputFilePath}（标题 {value.HeadingCount} 个{imageSummary}）";
            });
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
        OutputPath = string.Empty;
        LastResultText = string.Empty;
        PreviewText = string.Empty;
        StatusText = "请选择 Word 文件";
    }

    internal async Task LoadWordAsync(string filePath)
    {
        await RunBusyAsync("正在加载 Word...", async () =>
        {
            var result = await _wordToMarkdownService.LoadFileAsync(filePath);
            if (!result.IsSuccess)
            {
                ShowFailure(result.Message ?? "Word 文件加载失败");
                CurrentFile = null;
                return;
            }

            if (result.Data is null)
            {
                ShowFailure("Word 文件加载失败：未读取到文件信息");
                CurrentFile = null;
                return;
            }

            var loadedFile = result.Data;
            CurrentFile = loadedFile;
            OutputPath = BuildDefaultOutputPath(loadedFile.FilePath);
            LastResultText = string.Empty;
            PreviewText = string.Empty;
            StatusText = result.Message ?? "Word 文件加载完成";
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
        _messageService.SendMessage(message, "Word 转 Markdown");
    }

    partial void OnCurrentFileChanged(WordLoadedFileSummary? value)
    {
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(CanOperate));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(FileSizeText));
        NotifyCommandStates();
    }

    partial void OnOutputPathChanged(string value)
    {
        if (_isRestoring)
        {
            return;
        }

        SaveInputs();
    }

    partial void OnExportImagesChanged(bool value)
    {
        if (!_isRestoring)
        {
            SaveInputs();
        }

        OnPropertyChanged(nameof(ImageModeText));
    }

    // 只保存输出配置与图片模式；当前文件（CurrentFile）与预览文本不入存档
    private void SaveInputs()
    {
        _pageInputStore.Save(PageId, new Dictionary<string, string?>
        {
            [nameof(OutputPath)] = PageInputValues.Encode(OutputPath),
            [nameof(ExportImages)] = PageInputValues.Encode(ExportImages),
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
            if (saved.TryGetValue(nameof(OutputPath), out var outputPath) && outputPath is not null)
            {
                OutputPath = outputPath;
            }

            if (saved.TryGetValue(nameof(ExportImages), out var exportImages))
            {
                ExportImages = PageInputValues.DecodeBool(exportImages, ExportImages);
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

    partial void OnPreviewTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasPreview));
    }

    private void NotifyCommandStates()
    {
        ChooseWordFileCommand.NotifyCanExecuteChanged();
        ChooseOutputCommand.NotifyCanExecuteChanged();
        ConvertCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
    }

    private string BuildSuggestedFileName()
    {
        return CurrentFile is null
            ? "export.md"
            : $"{Path.GetFileNameWithoutExtension(CurrentFile.FileName)}.md";
    }

    private static string BuildDefaultOutputPath(string sourceFilePath)
    {
        var directory = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        var fileName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}.md";
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
