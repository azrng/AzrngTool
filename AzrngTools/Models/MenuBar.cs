using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models;

/// <summary>
/// 菜单
/// </summary>
public partial class MenuBar : ObservableObject
{
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
    /// 子项
    /// </summary>
    public List<MenuBar> Child { get; set; } = [];

    [ObservableProperty]
    private bool _isExpanded;
}
