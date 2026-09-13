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
    // 列表模板中逐项调用，画刷必须复用同一实例避免每次转换都分配新对象
    private static readonly SolidColorBrush TrueBrush = new(Colors.Green);
    private static readonly SolidColorBrush FalseBrush = new(Colors.Red);
    private static readonly SolidColorBrush DefaultBrush = new(Colors.Black);

    /// <summary>
    /// 转换值
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? TrueBrush : FalseBrush;
        }
        return DefaultBrush;
    }

    /// <summary>
    /// 反向转换值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}
