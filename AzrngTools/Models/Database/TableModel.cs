using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartSQL.UI.Models;

/// <summary>
/// 数据表模型
/// </summary>
public partial class TableModel : ObservableObject
{
    /// <summary>
    /// 表名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 架构名称
    /// </summary>
    [ObservableProperty]
    private string _schema = string.Empty;

    /// <summary>
    /// 表类型（表、视图等）
    /// </summary>
    [ObservableProperty]
    private string _tableType = string.Empty;

    /// <summary>
    /// 行数
    /// </summary>
    [ObservableProperty]
    private long _rowCount;

    /// <summary>
    /// 创建时间
    /// </summary>
    [ObservableProperty]
    private DateTime? _createTime;

    /// <summary>
    /// 修改时间
    /// </summary>
    [ObservableProperty]
    private DateTime? _modifyTime;

    /// <summary>
    /// 表注释/说明
    /// </summary>
    [ObservableProperty]
    private string _comment = string.Empty;
}
