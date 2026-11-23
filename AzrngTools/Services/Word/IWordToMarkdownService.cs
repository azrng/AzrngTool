using Azrng.Core.Results;
using AzrngTools.Models.Word;

namespace AzrngTools.Services.Word;

/// <summary>
/// Word（.docx）转 Markdown 能力：校验源文件、执行转换并写出 md 与图片资源。
/// </summary>
public interface IWordToMarkdownService
{
    /// <summary>
    /// 校验并读取 docx 文件摘要（存在性与扩展名校验，不解析内容）。
    /// </summary>
    Task<IResultModel<WordLoadedFileSummary>> LoadFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 把 docx 转换为 Markdown 并写出：图片按请求开关导出到 md 同级 .assets 目录（相对引用）或整体忽略。
    /// </summary>
    Task<IResultModel<WordMarkdownConvertResult>> ConvertAsync(WordMarkdownConvertRequest request, CancellationToken cancellationToken = default);
}
