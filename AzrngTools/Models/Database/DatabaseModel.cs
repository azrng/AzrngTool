using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AzrngTools.Models.Database;

/// <summary>
/// 数据库模型
/// </summary>
public partial class DatabaseModel : ObservableObject
{
    /// <summary>
    /// 数据库ID
    /// </summary>
    [ObservableProperty]
    private string _id = string.Empty;

    /// <summary>
    /// 数据库名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    [ObservableProperty]
    private DatabaseType _databaseType;

    /// <summary>
    /// 是否已连接
    /// </summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// 连接时间
    /// </summary>
    [ObservableProperty]
    private DateTime? _connectedTime;
}
