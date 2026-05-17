#nullable disable
using System.Text;
using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AzrngTools.ViewModels.Format;

/// <summary>
/// MIME类型查询
/// </summary>
public partial class MimeQueryPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public MimeQueryPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
        _searchText = string.Empty;
        _queryResult = string.Empty;
        InitializeMimeDatabase();
    }

    #region 属性

    /// <summary>
    /// 搜索文本
    /// </summary>
    [ObservableProperty]
    private string _searchText;

    /// <summary>
    /// 查询结果
    /// </summary>
    [ObservableProperty]
    private string _queryResult;

    #endregion

    /// <summary>
    /// MIME类型数据库
    /// </summary>
    private Dictionary<string, string> MimeDatabase { get; set; }

    /// <summary>
    /// 初始化MIME数据库
    /// </summary>
    private void InitializeMimeDatabase()
    {
        MimeDatabase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 文本类型
            {".txt", "text/plain"},
            {".html", "text/html"},
            {".htm", "text/html"},
            {".css", "text/css"},
            {".js", "text/javascript"},
            {".json", "application/json"},
            {".xml", "application/xml"},
            {".csv", "text/csv"},
            {".md", "text/markdown"},

            // 图片类型
            {".jpg", "image/jpeg"},
            {".jpeg", "image/jpeg"},
            {".png", "image/png"},
            {".gif", "image/gif"},
            {".bmp", "image/bmp"},
            {".ico", "image/x-icon"},
            {".svg", "image/svg+xml"},
            {".webp", "image/webp"},
            {".tiff", "image/tiff"},
            {".tif", "image/tiff"},

            // 音频类型
            {".mp3", "audio/mpeg"},
            {".wav", "audio/wav"},
            {".ogg", "audio/ogg"},
            {".flac", "audio/flac"},
            {".aac", "audio/aac"},
            {".m4a", "audio/mp4"},
            {".wma", "audio/x-ms-wma"},

            // 视频类型
            {".mp4", "video/mp4"},
            {".avi", "video/x-msvideo"},
            {".mov", "video/quicktime"},
            {".wmv", "video/x-ms-wmv"},
            {".flv", "video/x-flv"},
            {".webm", "video/webm"},
            {".mkv", "video/x-matroska"},
            {".m4v", "video/mp4"},
            {".3gp", "video/3gpp"},

            // 文档类型
            {".pdf", "application/pdf"},
            {".doc", "application/msword"},
            {".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"},
            {".xls", "application/vnd.ms-excel"},
            {".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"},
            {".ppt", "application/vnd.ms-powerpoint"},
            {".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation"},
            {".odt", "application/vnd.oasis.opendocument.text"},
            {".ods", "application/vnd.oasis.opendocument.spreadsheet"},
            {".odp", "application/vnd.oasis.opendocument.presentation"},
            {".rtf", "application/rtf"},
            {".tex", "application/x-tex"},

            // 压缩文件
            {".zip", "application/zip"},
            {".rar", "application/vnd.rar"},
            {".7z", "application/x-7z-compressed"},
            {".tar", "application/x-tar"},
            {".gz", "application/gzip"},
            {".bz2", "application/x-bzip2"},
            {".xz", "application/x-xz"},

            // 应用程序
            {".exe", "application/octet-stream"},
            {".dll", "application/octet-stream"},
            {".so", "application/octet-stream"},
            {".app", "application/octet-stream"},
            {".apk", "application/vnd.android.package-archive"},

            // 字体
            {".ttf", "font/ttf"},
            {".otf", "font/otf"},
            {".woff", "font/woff"},
            {".woff2", "font/woff2"},
            {".eot", "application/vnd.ms-fontobject"},

            // 其他
            {".bin", "application/octet-stream"},
            {".dat", "application/octet-stream"},
            {".cache", "application/octet-stream"}
        };
    }

    /// <summary>
    /// 文件扩展名查询
    /// </summary>
    [RelayCommand]
    private void QueryByExtension()
    {
        try
        {
            if (SearchText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入文件扩展名");
                return;
            }

            var ext = SearchText.Trim();
            var lastDotIndex = ext.LastIndexOf('.');
            if (lastDotIndex >= 0 && lastDotIndex < ext.Length - 1)
            {
                ext = ext[lastDotIndex..];
            }

            if (!ext.StartsWith("."))
            {
                ext = "." + ext;
            }

            ext = ext.ToLowerInvariant();

            if (MimeDatabase.TryGetValue(ext, out var mimeType))
            {
                QueryResult = $"文件扩展名：{ext}\r\nMIME类型：{mimeType}";
                _messageService.SendMessage("查询成功");
            }
            else
            {
                QueryResult = $"未找到扩展名 \"{ext}\" 对应的MIME类型";
                _messageService.SendMessage("未找到对应的MIME类型");
            }
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"查询失败：{ex.Message}");
        }
    }

    /// <summary>
    /// MIME类型反向查询
    /// </summary>
    [RelayCommand]
    private void QueryByMimeType()
    {
        try
        {
            if (SearchText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入MIME类型");
                return;
            }

            var mimeType = SearchText.Trim().ToLower();
            var matches = MimeDatabase.Where(kvp => kvp.Value.ToLower().Contains(mimeType)).ToList();

            if (matches.Any())
            {
                var sb = new StringBuilder($"MIME类型包含 \"{mimeType}\" 的文件扩展名：\r\n\r\n");
                foreach (var match in matches)
                {
                    sb.AppendLine($"{match.Key} -> {match.Value}");
                }
                QueryResult = sb.ToString();
                _messageService.SendMessage($"找到 {matches.Count} 个匹配项");
            }
            else
            {
                QueryResult = $"未找到包含 \"{mimeType}\" 的MIME类型";
                _messageService.SendMessage("未找到匹配的MIME类型");
            }
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"查询失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 显示所有MIME类型
    /// </summary>
    [RelayCommand]
    private void ShowAllMimeTypes()
    {
        try
        {
            var sb = new StringBuilder("所有文件扩展名及其MIME类型：\r\n\r\n");
            foreach (var kvp in MimeDatabase.OrderBy(kvp => kvp.Key))
            {
                sb.AppendLine($"{kvp.Key} -> {kvp.Value}");
            }
            QueryResult = sb.ToString();
            _messageService.SendMessage($"共 {MimeDatabase.Count} 个MIME类型");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"获取失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 清空
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        SearchText = string.Empty;
        QueryResult = string.Empty;
    }

    /// <summary>
    /// 复制结果
    /// </summary>
    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (QueryResult.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel != null)
            {
                await ClipboardHelper.SetTextAsync(topLevel, QueryResult);
            }
            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 获取 TopLevel
    /// </summary>
    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }
}
