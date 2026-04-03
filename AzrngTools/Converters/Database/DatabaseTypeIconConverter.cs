using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Azrng.Core.Model;
using AzrngTools.Models.Database;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 将数据库类型转换为对应的图标
/// </summary>
public class DatabaseTypeIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DatabaseType dbType)
        {
            return dbType switch
            {
                DatabaseType.SqlServer => "🔷",
                DatabaseType.MySql => "🐬",
                DatabaseType.PostgresSql => "🐘",
                DatabaseType.Sqlite => "📁",
                DatabaseType.Oracle => "🔶",
                DatabaseType.Dm => "🔴",
                _ => "📊"
            };
        }
        return "📊";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
