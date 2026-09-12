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
    public partial string Name { get; set; } = string.Empty;

    /// <summary>
    /// 数据库类型
    /// </summary>
    [ObservableProperty]
    public partial DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// 主机地址
    /// </summary>
    [ObservableProperty]
    public partial string Host { get; set; } = string.Empty;

    /// <summary>
    /// 端口
    /// </summary>
    [ObservableProperty]
    public partial int Port { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    [ObservableProperty]
    public partial string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>
    /// 数据库名称
    /// </summary>
    [ObservableProperty]
    public partial string Database { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用 Windows 身份验证（仅 SQL Server）
    /// </summary>
    [ObservableProperty]
    public partial bool UseWindowsAuthentication { get; set; }

    /// <summary>
    /// 最后使用时间
    /// </summary>
    [ObservableProperty]
    public partial DateTime? LastUsedTime { get; set; }

    /// <summary>
    /// 使用次数
    /// </summary>
    [ObservableProperty]
    public partial int UseCount { get; set; }

    /// <summary>
    /// 分组 ID
    /// </summary>
    [ObservableProperty]
    public partial string? GroupId { get; set; }

    /// <summary>
    /// 分组名称（显示用）
    /// </summary>
    [ObservableProperty]
    public partial string? GroupName { get; set; }

    /// <summary>
    /// 颜色标记（用于区分环境）
    /// </summary>
    [ObservableProperty]
    public partial string? Color { get; set; }

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
