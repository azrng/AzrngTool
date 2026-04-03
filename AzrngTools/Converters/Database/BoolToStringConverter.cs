using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 将 bool 值转换为字符串：true -> "升序", false -> "降序"
/// </summary>
public class BoolToStringConverter : IValueConverter
{
    /// <summary>
    /// 转换值
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "升序" : "降序";
        }
        return value;
    }

    /// <summary>
    /// 反向转换值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
