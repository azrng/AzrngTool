using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace ShellProto;

/// <summary>
/// 侧边导航条目（原型模拟数据，与主工程 MenuBar 结构对齐）。
/// </summary>
public partial class MenuItem : ObservableObject
{
    public MenuItem(string title)
    {
        Title = title;
    }

    public string Title { get; }

    public ObservableCollection<MenuItem>? Child { get; init; }

    [ObservableProperty]
    private bool _isExpanded;
}

/// <summary>
/// 原型主窗口视图模型：静态模拟数据驱动应用壳交互，无任何业务逻辑。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel()
    {
        GroupMenuItems =
        [
            new MenuItem("网络与接口")
            {
                IsExpanded = true,
                Child = [new MenuItem("接口调试")]
            },
            new MenuItem("文档工具")
            {
                Child = [new MenuItem("PDF管理")]
            },
            new MenuItem("生成类工具")
            {
                Child =
                [
                    new MenuItem("GUID生成器"),
                    new MenuItem("HASH"),
                    new MenuItem("HMAC HASH"),
                    new MenuItem("AES"),
                    new MenuItem("DES"),
                    new MenuItem("SM4"),
                    new MenuItem("RSA"),
                    new MenuItem("Json Schema生成"),
                    new MenuItem("JSON转C#实体类"),
                    new MenuItem("密码生成器")
                ]
            },
            new MenuItem("编码/解码类工具")
            {
                Child =
                [
                    new MenuItem("BASE64文本"),
                    new MenuItem("Base64转图片"),
                    new MenuItem("URL编码解码"),
                    new MenuItem("Unicode编码解码"),
                    new MenuItem("十六进制转换"),
                    new MenuItem("繁简体转换"),
                    new MenuItem("JWT解码器"),
                    new MenuItem("Gzip编码解码"),
                    new MenuItem("编码解码")
                ]
            },
            new MenuItem("格式化类工具")
            {
                Child =
                [
                    new MenuItem("文本压缩"),
                    new MenuItem("JSON格式化"),
                    new MenuItem("SQL操作"),
                    new MenuItem("XML转HTML"),
                    new MenuItem("JSON/YAML/XML互转"),
                    new MenuItem("正则表达式测试"),
                    new MenuItem("字数统计"),
                    new MenuItem("人民币大写转换"),
                    new MenuItem("MIME类型查询")
                ]
            },
            new MenuItem("其他")
            {
                Child =
                [
                    new MenuItem("Unix时间戳"),
                    new MenuItem("翻译")
                ]
            },
            new MenuItem("系统设置")
            {
                Child = [new MenuItem("关于")]
            }
        ];

        AppTitle = "AzrngTools";
        AppSubtitle = "开发工具集 · 支持分组浏览与搜索";
        TotalToolCount = 33;
        CategoryCount = GroupMenuItems.Count;
        CurrentPageTitle = "首页";
        CurrentPageSubtitle = "原型占位页：应用壳原型只覆盖主框架布局，不实现具体工具页。";
        UpdateSearchSummary();
    }

    [ObservableProperty]
    private ObservableCollection<MenuItem> _groupMenuItems;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _appTitle;

    [ObservableProperty]
    private string _appSubtitle;

    [ObservableProperty]
    private int _totalToolCount;

    [ObservableProperty]
    private int _categoryCount;

    [ObservableProperty]
    private string _searchSummary = string.Empty;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private string _currentPageTitle;

    [ObservableProperty]
    private string _currentPageSubtitle;

    public GridLength SidebarWidth => IsSidebarCollapsed ? new GridLength(72) : new GridLength(248);

    public string SidebarToggleIconPath => IsSidebarCollapsed
        ? "M9 5L16 12L9 19"
        : "M15 5L8 12L15 19";

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarToggleIconPath));
    }

    partial void OnSearchKeywordChanged(string value) => UpdateSearchSummary();

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    private void UpdateSearchSummary()
        => SearchSummary = string.IsNullOrWhiteSpace(SearchKeyword)
            ? $"全部分类 ({CategoryCount})"
            : $"搜索 \"{SearchKeyword}\" 的结果";
}
