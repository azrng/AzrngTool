using System.Globalization;

namespace AzrngTools.Services;

/// <summary>
/// 页面输入存档的标量编解码（CON-SETTINGS-001）：布尔、数值、枚举序号、时间统一转字符串存储，
/// 页面侧写入用 Encode、恢复用 TryDecode 兜底默认值，避免每页重复转换逻辑；文本原样存取。
/// </summary>
public static class PageInputValues
{
    public static string? Encode(string? value)
    {
        return value;
    }

    public static string Encode(bool value)
    {
        return value ? "true" : "false";
    }

    public static string Encode(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string Encode(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>枚举按序号十进制字符串存储，键值紧凑且与选项下拉的绑定方式一致。</summary>
    public static string Encode(Enum value)
    {
        return Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>时间用往返格式存储，恢复时不丢 Kind 与亚秒精度。</summary>
    public static string Encode(DateTime value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    public static bool DecodeBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var result) ? result : fallback;
    }

    public static int DecodeInt32(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    public static long DecodeInt64(string? value, long fallback)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : fallback;
    }

    /// <summary>枚举恢复：只接受已定义的序号值，超出定义范围（枚举缩水）按默认值兜底。</summary>
    public static T DecodeEnum<T>(string? value, T fallback) where T : struct, Enum
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return fallback;
        }

        // 先转成枚举再判定义：Enum.IsDefined 只接受枚举基础类型或字符串，不能直接传 long 装箱值
        var parsed = (T)Enum.ToObject(typeof(T), numeric);
        return Enum.IsDefined(parsed) ? parsed : fallback;
    }

    public static DateTime DecodeDateTime(string? value, DateTime fallback)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result)
            ? result
            : fallback;
    }
}
