using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 索引模型
/// </summary>
public partial class IndexModel : ObservableObject
{
    /// <summary>
    /// 索引名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 索引类型（聚集、非聚集等）
    /// </summary>
    [ObservableProperty]
    private string _indexType = string.Empty;

    /// <summary>
    /// 是否唯一
    /// </summary>
    [ObservableProperty]
    private bool _isUnique;

    /// <summary>
    /// 是否为主键
    /// </summary>
    [ObservableProperty]
    private bool _isPrimaryKey;

    /// <summary>
    /// 索引列
    /// </summary>
    [ObservableProperty]
    private string _columns = string.Empty;

    /// <summary>
    /// 是否升序
    /// </summary>
    [ObservableProperty]
    private bool _isAscending;
}
