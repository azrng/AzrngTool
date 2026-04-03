using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Azrng.Core.Model;
using AzrngTools.Models.Database;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 将 DatabaseType 转换为布尔值（用于判断是否匹配指定类型）
/// </summary>
public class DatabaseTypeToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DatabaseType dbType && parameter is string param)
        {
            return param.ToLower() switch
            {
                "sqlserver" => dbType == DatabaseType.SqlServer,
                "MySql" => dbType == DatabaseType.MySql,
                "PostgresSql" => dbType == DatabaseType.PostgresSql,
                "Sqlite" => dbType == DatabaseType.Sqlite,
                "oracle" => dbType == DatabaseType.Oracle,
                "Dm" => dbType == DatabaseType.Dm,
                _ => false
            };
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
