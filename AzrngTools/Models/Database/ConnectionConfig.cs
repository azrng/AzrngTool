using Azrng.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using AzrngTools.Utils.Database;

namespace AzrngTools.Models.Database;

/// <summary>
/// 数据库连接配置
/// </summary>
public partial class ConnectionConfig : ObservableObject
{
    /// <summary>
    /// 连接名称
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    [ObservableProperty]
    private DatabaseType _databaseType;

    /// <summary>
    /// 主机地址
    /// </summary>
    [ObservableProperty]
    private string _host = string.Empty;

    /// <summary>
    /// 端口
    /// </summary>
    [ObservableProperty]
    private int _port;

    /// <summary>
    /// 用户名
    /// </summary>
    [ObservableProperty]
    private string _username = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>
    /// 数据库名称
    /// </summary>
    [ObservableProperty]
    private string _database = string.Empty;

    /// <summary>
    /// 是否使用 Windows 身份验证（仅 SQL Server）
    /// </summary>
    [ObservableProperty]
    private bool _useWindowsAuthentication;

    /// <summary>
    /// 最后使用时间
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastUsedTime;

    /// <summary>
    /// 使用次数
    /// </summary>
    [ObservableProperty]
    private int _useCount;

    /// <summary>
    /// 分组 ID
    /// </summary>
    [ObservableProperty]
    private string? _groupId;

    /// <summary>
    /// 分组名称（显示用）
    /// </summary>
    [ObservableProperty]
    private string? _groupName;

    /// <summary>
    /// 颜色标记（用于区分环境）
    /// </summary>
    [ObservableProperty]
    private string? _color;

    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? DatabaseType.ToString()
        : $"{Name} ( {DatabaseType} )";

    /// <summary>
    /// 加密密码
    /// </summary>
    /// <returns>加密后的密码</returns>
    public string GetEncryptedPassword()
    {
        return EncryptHelper.Encode(Password);
    }

    /// <summary>
    /// 解密密码
    /// </summary>
    /// <param name="encryptedPassword">加密的密码</param>
    public void SetDecryptedPassword(string encryptedPassword)
    {
        Password = EncryptHelper.Decode(encryptedPassword);
    }

    /// <summary>
    /// 转换为连接字符串
    /// </summary>
    public string ToConnectionString()
    {
        return DatabaseType switch
        {
            DatabaseType.SqlServer => UseWindowsAuthentication
                ? $"Server={Host},{Port};Database={Database};Integrated Security=true;"
                : $"Server={Host},{Port};Database={Database};User Id={Username};Password={Password};",
            DatabaseType.MySql => $"Server={Host};Port={Port};Database={Database};User Id={Username};Password={Password};",
            DatabaseType.PostgresSql => $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};",
            DatabaseType.Oracle => $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={Host})(PORT={Port}))(CONNECT_DATA=(SERVICE_NAME={Database})));User Id={Username};Password={Password};",
            DatabaseType.Sqlite => $"Data Source={Database};",
            DatabaseType.Dm => $"Server={Host}:{Port};DATABASE={Database};UID={Username};PWD={Password};",
            _ => throw new NotSupportedException($"Database type {DatabaseType} is not supported.")
        };
    }

    /// <summary>
    /// 获取数据库类型图标
    /// </summary>
    public string DatabaseTypeIcon => DatabaseType switch
    {
        DatabaseType.SqlServer => "MS",
        DatabaseType.MySql => "MY",
        DatabaseType.PostgresSql => "PG",
        DatabaseType.Sqlite => "SQ",
        DatabaseType.Oracle => "OR",
        DatabaseType.Dm => "DM",
        _ => "DB"
    };

    /// <summary>
    /// 获取数据库类型对应的背景色
    /// </summary>
    public string DatabaseTypeBackgroundColor => DatabaseType switch
    {
        DatabaseType.SqlServer => "#EEF4FF",
        DatabaseType.MySql => "#EEFBFF",
        DatabaseType.PostgresSql => "#EDF5FF",
        DatabaseType.Sqlite => "#F2F7FB",
        DatabaseType.Oracle => "#FFF2F0",
        DatabaseType.Dm => "#FFF1F1",
        _ => "#F5F7FB"
    };

    /// <summary>
    /// 更新使用统计
    /// </summary>
    public void UpdateUsageStats()
    {
        UseCount++;
        LastUsedTime = DateTime.Now;
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnDatabaseTypeChanged(DatabaseType value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
