using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SmartSQL.UI.Converters;

/// <summary>
/// 将枚举值转换为索引（用于 ComboBox SelectedIndex 绑定）
/// </summary>
public class EnumToIndexConverter : IValueConverter
{
    /// <summary>
    /// 转换值：枚举值 -> 索引
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Enum enumValue)
        {
            // 获取枚举的整数值
            var intValue = System.Convert.ToInt32(enumValue);
            // 转换为索引（枚举值 - 1）
            return intValue - 1;
        }
        return 0;
    }

    /// <summary>
    /// 反向转换值：索引 -> 枚举值
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            // 转换为枚举值（索引 + 1）
            return System.Convert.ChangeType(index + 1, targetType);
        }
        return System.Convert.ChangeType(1, targetType);
    }
}
