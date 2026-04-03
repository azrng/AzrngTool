using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 视图模型
/// </summary>
public partial class ViewModel : ObservableObject
{
    /// <summary>
    /// 视图名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 架构名称
    /// </summary>
    [ObservableProperty]
    private string _schema = string.Empty;

    /// <summary>
    /// 视图定义
    /// </summary>
    [ObservableProperty]
    private string _definition = string.Empty;

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
    /// 视图注释/说明
    /// </summary>
    [ObservableProperty]
    private string _comment = string.Empty;
}
