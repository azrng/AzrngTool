using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SmartSQL.UI.Converters;

/// <summary>
/// 将 bool 值反转：true -> false, false -> true
/// </summary>
public class InvertedBoolConverter : IValueConverter
{
    /// <summary>
    /// 转换值
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    /// <summary>
    /// 反向转换值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}
