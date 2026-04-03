using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 当值大于 0 时返回 true，否则返回 false
/// </summary>
public class GreaterThanZeroConverter : IValueConverter
{
    /// <summary>
    /// 转换值
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal)
        {
            return intVal > 0;
        }
        if (value is long longVal)
        {
            return longVal > 0;
        }
        return false;
    }

    /// <summary>
    /// 反向转换值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
