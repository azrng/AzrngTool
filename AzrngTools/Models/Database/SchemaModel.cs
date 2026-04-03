using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 数据库架构模型
/// </summary>
public partial class SchemaModel : ObservableObject
{
    /// <summary>
    /// 架构名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 架构所有者
    /// </summary>
    [ObservableProperty]
    private string _owner = string.Empty;

    /// <summary>
    /// 表数量
    /// </summary>
    [ObservableProperty]
    private int _tableCount;

    /// <summary>
    /// 是否为默认架构
    /// </summary>
    [ObservableProperty]
    private bool _isDefault;
}
