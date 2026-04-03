using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 连接分组模型
/// </summary>
public partial class ConnectionGroup : ObservableObject
{
    /// <summary>
    /// 分组 ID
    /// </summary>
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 分组名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 分组描述
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// 分组颜色（用于 UI 标记）
    /// </summary>
    [ObservableProperty]
    private string _color = "#E3EFE8";

    /// <summary>
    /// 是否为默认分组
    /// </summary>
    [ObservableProperty]
    private bool _isDefault;
}
