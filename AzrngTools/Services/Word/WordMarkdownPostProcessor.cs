using System.Text.RegularExpressions;

namespace AzrngTools.Services.Word;

/// <summary>
/// 转换输出的纯文本后处理：清洗已知噪声，不改变 Markdown 语义。
/// </summary>
internal static partial class WordMarkdownPostProcessor
{
    // Word 目录书签导出的空锚点（如 <a id="_Toc16091304"></a>）：无 href 无文本，对阅读器是纯噪声
    [GeneratedRegex("""<a id="[^"]*"></a>""")]
    private static partial Regex EmptyAnchorPattern();

    /// <summary>
    /// 清洗 Markdown 源文本：移除 Word 书签空锚点等噪声。
    /// </summary>
    public static string Clean(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return string.Empty;
        }

        return EmptyAnchorPattern().Replace(markdown, string.Empty);
    }
}
