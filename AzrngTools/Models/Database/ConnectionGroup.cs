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
    public partial string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// 分组名称
    /// </summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// 分组描述
    /// </summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>
    /// 分组颜色（用于 UI 标记）
    /// </summary>
    [ObservableProperty]
    public partial string Color { get; set; } = "#E3EFE8";

    /// <summary>
    /// 是否为默认分组
    /// </summary>
    [ObservableProperty]
    public partial bool IsDefault { get; set; }
}
