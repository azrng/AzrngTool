using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 将 null 转换为 false，非 null 转换为 true
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    /// <summary>
    /// 转换值
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    /// <summary>
    /// 反向转换值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
