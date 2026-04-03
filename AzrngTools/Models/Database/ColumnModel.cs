using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 列模型
/// </summary>
public partial class ColumnModel : ObservableObject
{
    /// <summary>
    /// 列名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 数据类型
    /// </summary>
    [ObservableProperty]
    private string _dataType = string.Empty;

    /// <summary>
    /// 长度
    /// </summary>
    [ObservableProperty]
    private int? _length;

    /// <summary>
    /// 是否可为空
    /// </summary>
    [ObservableProperty]
    private bool _isNullable;

    /// <summary>
    /// 默认值
    /// </summary>
    [ObservableProperty]
    private string _defaultValue = string.Empty;

    /// <summary>
    /// 是否为主键
    /// </summary>
    [ObservableProperty]
    private bool _isPrimaryKey;

    /// <summary>
    /// 是否为自增列
    /// </summary>
    [ObservableProperty]
    private bool _isIdentity;

    /// <summary>
    /// 列序号
    /// </summary>
    [ObservableProperty]
    private int _ordinalPosition;

    /// <summary>
    /// 列注释/说明
    /// </summary>
    [ObservableProperty]
    private string _comment = string.Empty;
}
