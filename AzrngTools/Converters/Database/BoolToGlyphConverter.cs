using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 将布尔值转换为 Unicode 字形符号（✓/✗）
/// </summary>
public class BoolToGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "\u2713" : "\u2717";
        }
        return "\u2717";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
