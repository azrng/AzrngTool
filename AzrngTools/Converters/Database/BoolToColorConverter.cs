using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 将布尔值转换为颜色
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// 转换值
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
        }
        return new SolidColorBrush(Colors.Black);
    }

    /// <summary>
    /// 反向转换值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
