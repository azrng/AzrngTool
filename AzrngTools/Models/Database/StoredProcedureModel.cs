using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 存储过程模型
/// </summary>
public partial class StoredProcedureModel : ObservableObject
{
    /// <summary>
    /// 存储过程名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 架构名称
    /// </summary>
    [ObservableProperty]
    private string _schema = string.Empty;

    /// <summary>
    /// 存储过程定义
    /// </summary>
    [ObservableProperty]
    private string _definition = string.Empty;

    /// <summary>
    /// 参数列表
    /// </summary>
    [ObservableProperty]
    private string _parameters = string.Empty;

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
    /// 存储过程注释/说明
    /// </summary>
    [ObservableProperty]
    private string _comment = string.Empty;

    /// <summary>
    /// 例程类型
    /// </summary>
    [ObservableProperty]
    private string _routineType = "PROCEDURE";

    public bool IsFunction => string.Equals(RoutineType, "FUNCTION", StringComparison.OrdinalIgnoreCase);

    public string ObjectTypeDisplay => IsFunction ? "函数" : "存储过程";

    partial void OnRoutineTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsFunction));
        OnPropertyChanged(nameof(ObjectTypeDisplay));
    }
}
