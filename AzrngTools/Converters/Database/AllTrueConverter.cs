using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AzrngTools.Converters.Database;

/// <summary>
/// 多值转换器：当所有值都为 true 时返回 true
/// </summary>
public class AllTrueConverter : IMultiValueConverter
{
    /// <summary>
    /// 转换多个值：当所有值都为 true 时返回 true
    /// </summary>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count == 0)
        {
            return false;
        }

        // 检查所有值是否都为 true
        return values.All(value =>
        {
            // 处理 BindingNotification
            if (value is BindingNotification notification)
            {
                return false;
            }

            // 处理布尔值
            if (value is bool boolValue)
            {
                return boolValue;
            }

            // 处理字符串
            if (value is string stringValue)
            {
                return !string.IsNullOrWhiteSpace(stringValue);
            }

            // 处理可空类型
            if (value == null)
            {
                return false;
            }

            return true;
        });
    }

    /// <summary>
    /// 不支持反向转换
    /// </summary>
    public IList<object?>? ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ConvertBack is not supported for AllTrueConverter.");
    }
}
