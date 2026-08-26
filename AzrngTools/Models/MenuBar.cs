using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models;

/// <summary>
/// 菜单
/// </summary>
public partial class MenuBar : ObservableObject
{
    private const string HomeIconPath = "M3 11.5L12 3L21 11.5V21H14V15H10V21H3Z";
    private const string DatabaseIconPath = "M4 5C4 3.9 7.58 3 12 3C16.42 3 20 3.9 20 5V19C20 20.1 16.42 21 12 21C7.58 21 4 20.1 4 19V5ZM6 6.5C7.4 7.05 9.52 7.3 12 7.3C14.48 7.3 16.6 7.05 18 6.5M6 11C7.4 11.55 9.52 11.8 12 11.8C14.48 11.8 16.6 11.55 18 11M6 15.5C7.4 16.05 9.52 16.3 12 16.3C14.48 16.3 16.6 16.05 18 15.5";
    private const string NetworkIconPath = "M5 4H19V9H5V4ZM5 15H19V20H5V15ZM8 9V15M16 9V15M8 6.5H8.01M8 17.5H8.01";
    private const string DocumentIconPath = "M6 3H14L19 8V21H6V3ZM14 3V8H19M9 12H16M9 16H16";
    private const string GenerateIconPath = "M12 3L14.2 8.1L19.8 8.7L15.6 12.4L16.8 18L12 15L7.2 18L8.4 12.4L4.2 8.7L9.8 8.1L12 3Z";
    private const string EncodeIconPath = "M4 7H17M13 3L17 7L13 11M20 17H7M11 13L7 17L11 21";
    private const string FormatIconPath = "M8 4H5V20H8M16 4H19V20H16M11 7L13 12L11 17";
    private const string OtherIconPath = "M12 3V21M3 12H21M5.6 5.6L18.4 18.4M18.4 5.6L5.6 18.4";
    private const string SettingsIconPath = "M12 8.5A3.5 3.5 0 1 0 12 15.5A3.5 3.5 0 1 0 12 8.5ZM12 3L13.2 5.1L15.6 5.4L17.1 3.9L19.1 5.9L17.6 7.4L17.9 9.8L20 11L19.3 13.6L17 14.1L15.8 16.2L16.4 18.4L13.8 19.3L12 17.8L10.2 19.3L7.6 18.4L8.2 16.2L7 14.1L4.7 13.6L4 11L6.1 9.8L6.4 7.4L4.9 5.9L6.9 3.9L8.4 5.4L10.8 5.1L12 3Z";
    private const string ToolIconPath = "M5 5H19V19H5V5ZM8 8H16M8 12H16M8 16H13";

    public MenuBar() { }

    public MenuBar(string title, Type menuType, string toolTip = "")
    {
        Title = title;
        ToolTip = toolTip;
        MenuType = menuType;
    }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 菜单类型
    /// </summary>
    public Type? MenuType { get; set; }

    /// <summary>
    /// 悬浮的值
    /// </summary>
    public string ToolTip { get; set; } = string.Empty;

    /// <summary>
    /// 菜单图标路径。分组使用语义图标，具体工具使用统一工具图标。
    /// </summary>
    public string IconPath => Title switch
    {
        "首页" => HomeIconPath,
        "数据库工具" => DatabaseIconPath,
        "网络与接口" => NetworkIconPath,
        "文档工具" => DocumentIconPath,
        "生成类工具" => GenerateIconPath,
        "编码/解码类工具" => EncodeIconPath,
        "格式化类工具" => FormatIconPath,
        "其他" => OtherIconPath,
        "系统设置" => SettingsIconPath,
        _ => ToolIconPath
    };

    /// <summary>
    /// 子项
    /// </summary>
    public List<MenuBar> Child { get; set; } = [];

    [ObservableProperty]
    private bool _isExpanded;
}
