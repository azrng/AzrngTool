using AzrngTools.Models.Format;

namespace AzrngTools.Services.Format;

public interface IFormatConversionService
{
    /// <summary>
    /// 将源内容从一种格式转换为另一种格式；解析失败时抛出 ArgumentException 并携带行号信息。
    /// </summary>
    string Convert(string source, FormatLanguage from, FormatLanguage to);

    /// <summary>
    /// 格式化指定格式的内容（缩进美化），原地返回。
    /// </summary>
    string Format(string source, FormatLanguage language);

    /// <summary>
    /// 校验内容是否符合指定格式的语法。
    /// </summary>
    ConversionCheckResult Validate(string source, FormatLanguage language);
}
