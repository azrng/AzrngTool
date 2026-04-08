using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Styling;
using AzrngTools.Services;
using AzrngTools.ViewModels.Encode;
using AzrngTools.ViewModels.Encrypts;
using AzrngTools.ViewModels.Format;
using AzrngTools.ViewModels.Other;
using AzrngTools.ViewModels.Setting;
using AzrngTools.ViewModels.TextHandle;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using DbWorkbenchViewModel = AzrngTools.ViewModels.Database.MainWindowViewModel;

namespace AzrngTools.ViewModels;

/// <summary>
/// 主窗口视图模型。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxCommonEntryCount = 3;

    private static readonly Type[] DefaultCommonToolTypes =
    [
        typeof(GuidShowPageViewModel),
        typeof(JsonPageViewModel),
        typeof(UrlEncodePageViewModel)
    ];

    private readonly IServiceProvider _serviceProvider;
    private readonly IThemePreferenceService _themePreferenceService;
    private readonly IToolUsageStatsService _toolUsageStatsService;
    private readonly Dictionary<string, MenuBar> _toolMenuLookup = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _groupExpansionStates = new(StringComparer.Ordinal);

    private List<MenuBar> _allRootMenus = [];
    private List<MenuBar> _allGroupMenus = [];
    private MenuBar? _homeMenu;
    private bool _suppressUsageTracking;

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        IToolUsageStatsService toolUsageStatsService,
        IThemePreferenceService themePreferenceService)
    {
        _serviceProvider = serviceProvider;
        _toolUsageStatsService = toolUsageStatsService;
        _themePreferenceService = themePreferenceService;

        CreateMenuBars();
        BuildToolLookup();
        ApplyMenuFilter();

        AppTitle = "AzrngTools";
        AppSubtitle = "开发工具集 · 支持分组浏览与搜索";
        TotalToolCount = _allGroupMenus.Sum(group => group.Child?.Count ?? 0);
        CategoryCount = _allGroupMenus.Count;
        IsDarkThemeEnabled = Application.Current?.RequestedThemeVariant == ThemeVariant.Dark
            || Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

        SelectedListItem = _homeMenu is null
            ? RootMenuItems.FirstOrDefault()
            : CloneMenu(_homeMenu);
    }

    [ObservableProperty]
    private ObservableCollection<MenuBar> _rootMenuItems = [];

    [ObservableProperty]
    private ObservableCollection<MenuBar> _groupMenuItems = [];

    [ObservableProperty]
    private MenuBar? _selectedListItem;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _appTitle = string.Empty;

    [ObservableProperty]
    private string _appSubtitle = string.Empty;

    [ObservableProperty]
    private string _currentPageTitle = string.Empty;

    [ObservableProperty]
    private string _currentPageSubtitle = string.Empty;

    [ObservableProperty]
    private int _totalToolCount;

    [ObservableProperty]
    private int _visibleToolCount;

    [ObservableProperty]
    private int _categoryCount;

    [ObservableProperty]
    private string _searchSummary = string.Empty;

    [ObservableProperty]
    private bool _isDarkThemeEnabled;

    private void CreateMenuBars()
    {
        var menus = new List<MenuBar>
        {
            new("首页", typeof(OverviewPageViewModel)),
            new MenuBar
            {
                Title = "数据库工具",
                Child =
                [
                    new MenuBar("数据库工作台", typeof(DbWorkbenchViewModel))
                ]
            },
            new MenuBar
            {
                Title = "生成类工具",
                Child =
                [
                    new MenuBar("GUID生成器", typeof(GuidShowPageViewModel)),
                    new MenuBar("HASH", typeof(HashPageViewModel)),
                    new MenuBar("HMAC HASH", typeof(HmacHashPageViewModel)),
                    new MenuBar("AES", typeof(AesEncryptPageViewModel)),
                    new MenuBar("DES", typeof(DesEncryptPageViewModel)),
                    new MenuBar("SM4", typeof(Sm4EncryptPageViewModel)),
                    new MenuBar("RSA", typeof(RsaEncryptPageViewModel)),
                    new MenuBar("Json Schema生成", typeof(JsonSchemaPageViewModel)),
                    new MenuBar("JSON转C#实体类", typeof(JsonToCsharpPageViewModel)),
                    new MenuBar("密码生成器", typeof(PasswordGeneratorPageViewModel))
                ]
            },
            new MenuBar
            {
                Title = "编码/解码类工具",
                Child =
                [
                    new MenuBar("BASE64文本", typeof(Base64EncodePageViewModel)),
                    new MenuBar("Base64转图片", typeof(Base64ToImagePageViewModel)),
                    new MenuBar("URL编码解码", typeof(UrlEncodePageViewModel)),
                    new MenuBar("Unicode编码解码", typeof(UnicodeEncodePageViewModel)),
                    new MenuBar("十六进制转换", typeof(HexEncodePageViewModel)),
                    new MenuBar("繁简体转换", typeof(ChineseConvertPageViewModel)),
                    new MenuBar("JWT解码器", typeof(JwtEncodePageViewModel)),
                    new MenuBar("Gzip编码解码", typeof(GzipEncodePageViewModel)),
                    new MenuBar("编码解码", typeof(EncodePageViewModel))
                ]
            },
            new MenuBar
            {
                Title = "格式化类工具",
                Child =
                [
                    new MenuBar("文本压缩", typeof(StringPageViewModel)),
                    new MenuBar("JSON格式化", typeof(JsonPageViewModel)),
                    new MenuBar("SQL操作", typeof(SqlFormatPageViewModel)),
                    new MenuBar("XML转HTML", typeof(XmlToHtmlPageViewModel)),
                    new MenuBar("正则表达式测试", typeof(RegexAnalysisViewModel)),
                    new MenuBar("字数统计", typeof(WordCountPageViewModel)),
                    new MenuBar("人民币大写转换", typeof(RMBConvertPageViewModel)),
                    new MenuBar("MIME类型查询", typeof(MimeQueryPageViewModel))
                ]
            },
            new MenuBar
            {
                Title = "其他",
                Child =
                [
                    new MenuBar("Unix时间戳", typeof(UnixTimestampPageViewModel)),
                    new MenuBar("翻译", typeof(TranslatorPageViewModel))
                ]
            },
            new MenuBar
            {
                Title = "系统设置",
                Child =
                [
                    new MenuBar("硬件信息", typeof(HardwarePageViewModel)),
                    new MenuBar("关于", typeof(AboutPageViewModel))
                ]
            }
        };

        _allRootMenus = menus.Where(menu => menu.MenuType is not null).ToList();
        _allGroupMenus = menus.Where(menu => menu.Child?.Count > 0).ToList();
        _homeMenu = _allRootMenus.FirstOrDefault(menu => menu.MenuType == typeof(OverviewPageViewModel));
    }

    private void BuildToolLookup()
    {
        _toolMenuLookup.Clear();

        foreach (var menu in _allRootMenus.Concat(_allGroupMenus.SelectMany(group => group.Child ?? [])))
        {
            if (menu.MenuType is null)
            {
                continue;
            }

            _toolMenuLookup[GetMenuKey(menu)] = menu;
        }
    }

    partial void OnSearchKeywordChanged(string value)
    {
        ApplyMenuFilter();
    }

    [RelayCommand]
    private void SelectMenu(MenuBar menu)
    {
        if (menu?.MenuType is null)
        {
            return;
        }

        SelectedListItem = menu;
    }

    [RelayCommand]
    private void GoHome()
    {
        if (_homeMenu is null)
        {
            return;
        }

        SetSelectedListItemSilently(CloneMenu(_homeMenu));
    }

    private void ApplyMenuFilter()
    {
        var hasSearchKeyword = !string.IsNullOrWhiteSpace(SearchKeyword);
        if (!hasSearchKeyword)
        {
            CaptureGroupExpansionStates();
        }

        DetachGroupExpansionHandlers();

        RootMenuItems = new ObservableCollection<MenuBar>(BuildCommonEntries(SearchKeyword));

        GroupMenuItems = new ObservableCollection<MenuBar>(_allGroupMenus
            .Select(menu => CloneGroupForSearch(menu, SearchKeyword, hasSearchKeyword))
            .Where(menu => menu is not null)!);

        AttachGroupExpansionHandlers();

        VisibleToolCount = CountVisibleMenus(SearchKeyword);
        SearchSummary = VisibleToolCount == 0
            ? "未找到匹配工具"
            : string.IsNullOrWhiteSpace(SearchKeyword)
                ? $"当前展示 {VisibleToolCount} 个工具"
                : $"已匹配 {VisibleToolCount} 个工具";

        if (SelectedListItem is null)
        {
            return;
        }

        var selectedClone = FindVisibleMenu(SelectedListItem);
        if (selectedClone is not null && !ReferenceEquals(selectedClone, SelectedListItem))
        {
            SetSelectedListItemSilently(selectedClone);
            return;
        }

        if (selectedClone is null)
        {
            if (SelectedListItem?.MenuType == typeof(OverviewPageViewModel))
            {
                return;
            }

            SetSelectedListItemSilently(RootMenuItems.FirstOrDefault() ??
                                        GroupMenuItems.SelectMany(group => group.Child ?? []).FirstOrDefault());
        }
    }

    partial void OnSelectedListItemChanged(MenuBar? value)
    {
        if (value?.MenuType is null)
        {
            return;
        }

        var service = _serviceProvider.GetService(value.MenuType);
        if (service is not null)
        {
            CurrentPage = service;
            UpdatePageHeader(value);
        }

        if (_suppressUsageTracking || value.MenuType == typeof(OverviewPageViewModel))
        {
            return;
        }

        _toolUsageStatsService.RecordToolUsage(GetMenuKey(value), value.Title);
        SetSelectionRefreshSilently();
    }

    private void SetSelectionRefreshSilently()
    {
        _suppressUsageTracking = true;
        try
        {
            ApplyMenuFilter();
        }
        finally
        {
            _suppressUsageTracking = false;
        }
    }

    private void SetSelectedListItemSilently(MenuBar? menu)
    {
        if (menu is null)
        {
            return;
        }

        _suppressUsageTracking = true;
        try
        {
            SelectedListItem = menu;
        }
        finally
        {
            _suppressUsageTracking = false;
        }
    }

    private IEnumerable<MenuBar> BuildCommonEntries(string keyword)
    {
        var addedKeys = new HashSet<string>(StringComparer.Ordinal);
        return GetRankedCommonMenus(keyword, addedKeys, MaxCommonEntryCount).ToList();
    }

    private IEnumerable<MenuBar> GetRankedCommonMenus(string keyword, HashSet<string> addedKeys, int maxCount)
    {
        foreach (var key in _toolUsageStatsService.GetTopToolKeys(_toolMenuLookup.Count))
        {
            if (!TryBuildCommonMenu(key, keyword, addedKeys, out var menu))
            {
                continue;
            }

            if (menu is null)
            {
                continue;
            }

            yield return menu;
            if (addedKeys.Count >= maxCount)
            {
                yield break;
            }
        }

        foreach (var defaultToolType in DefaultCommonToolTypes)
        {
            if (!TryBuildCommonMenu(GetMenuKey(defaultToolType), keyword, addedKeys, out var menu))
            {
                continue;
            }

            if (menu is null)
            {
                continue;
            }

            yield return menu;
            if (addedKeys.Count >= maxCount)
            {
                yield break;
            }
        }
    }

    private bool TryBuildCommonMenu(string key, string keyword, HashSet<string> addedKeys, out MenuBar? menu)
    {
        menu = null;

        if (string.IsNullOrWhiteSpace(key) || addedKeys.Contains(key))
        {
            return false;
        }

        if (!_toolMenuLookup.TryGetValue(key, out var source) || source.MenuType == typeof(OverviewPageViewModel))
        {
            return false;
        }

        if (!MatchesMenu(source, keyword))
        {
            return false;
        }

        addedKeys.Add(key);
        menu = CloneMenu(source);
        return true;
    }

    private int CountVisibleMenus(string keyword)
    {
        var visibleKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var menu in GroupMenuItems.SelectMany(group => group.Child ?? []))
        {
            visibleKeys.Add(GetMenuKey(menu));
        }

        return visibleKeys.Count;
    }

    private void UpdatePageHeader(MenuBar menu)
    {
        CurrentPageTitle = menu.Title;

        if (menu.Title == "首页")
        {
            CurrentPageSubtitle = "开发工具总览与常用入口，适合快速进入常用功能。";
            return;
        }

        var parent = _allGroupMenus.FirstOrDefault(group =>
            group.Child?.Any(child => child.Title == menu.Title && child.MenuType == menu.MenuType) == true);

        CurrentPageSubtitle = parent is null
            ? "当前工具页面。"
            : $"{parent.Title} · 直接可用的单页工具。";
    }

    private static bool MatchesMenu(MenuBar menu, string keyword)
    {
        return string.IsNullOrWhiteSpace(keyword)
               || menu.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static MenuBar CloneMenu(MenuBar source)
    {
        return new MenuBar
        {
            Title = source.Title,
            MenuType = source.MenuType,
            ToolTip = source.ToolTip
        };
    }

    private void CaptureGroupExpansionStates()
    {
        if (GroupMenuItems is null)
        {
            return;
        }

        foreach (var group in GroupMenuItems)
        {
            _groupExpansionStates[GetGroupKey(group)] = group.IsExpanded;
        }
    }

    private void AttachGroupExpansionHandlers()
    {
        if (GroupMenuItems is null)
        {
            return;
        }

        foreach (var group in GroupMenuItems)
        {
            group.PropertyChanged += OnGroupPropertyChanged;
        }
    }

    private void DetachGroupExpansionHandlers()
    {
        if (GroupMenuItems is null)
        {
            return;
        }

        foreach (var group in GroupMenuItems)
        {
            group.PropertyChanged -= OnGroupPropertyChanged;
        }
    }

    private void OnGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MenuBar.IsExpanded) || sender is not MenuBar currentGroup || !currentGroup.IsExpanded)
        {
            return;
        }

        foreach (var group in GroupMenuItems)
        {
            if (ReferenceEquals(group, currentGroup) || !group.IsExpanded)
            {
                continue;
            }

            group.IsExpanded = false;
        }

        CaptureGroupExpansionStates();
    }

    private MenuBar? CloneGroupForSearch(MenuBar source, string keyword, bool hasSearchKeyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new MenuBar
            {
                IsExpanded = _groupExpansionStates.GetValueOrDefault(GetGroupKey(source)),
                Title = source.Title,
                ToolTip = source.ToolTip,
                Child = source.Child?.Select(CloneMenu).ToList() ?? []
            };
        }

        if (source.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return new MenuBar
            {
                IsExpanded = true,
                Title = source.Title,
                ToolTip = source.ToolTip,
                Child = source.Child?.Select(CloneMenu).ToList() ?? []
            };
        }

        var matches = source.Child?
            .Where(menu => MatchesMenu(menu, keyword))
            .Select(CloneMenu)
            .ToList();

        if (matches is null || matches.Count == 0)
        {
            return null;
        }

        return new MenuBar
        {
            IsExpanded = hasSearchKeyword,
            Title = source.Title,
            ToolTip = source.ToolTip,
            Child = matches
        };
    }

    private static string GetMenuKey(MenuBar menu)
    {
        return menu.MenuType?.FullName ?? menu.Title;
    }

    private static string GetMenuKey(Type menuType)
    {
        return menuType.FullName ?? menuType.Name;
    }

    private static string GetGroupKey(MenuBar menu)
    {
        return menu?.Title ?? string.Empty;
    }

    private static bool IsSameMenu(MenuBar? left, MenuBar? right)
    {
        return left?.Title == right?.Title && left?.MenuType == right?.MenuType;
    }

    private MenuBar? FindVisibleMenu(MenuBar target)
    {
        return RootMenuItems.FirstOrDefault(menu => IsSameMenu(menu, target))
               ?? GroupMenuItems.SelectMany(group => group.Child ?? []).FirstOrDefault(menu => IsSameMenu(menu, target));
    }

    [RelayCommand]
    private void ToggleCheckedChanged()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var requestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;

        app.RequestedThemeVariant = requestedThemeVariant;
        IsDarkThemeEnabled = requestedThemeVariant == ThemeVariant.Dark;
        _themePreferenceService.SaveRequestedThemeVariant(requestedThemeVariant);
    }
}
