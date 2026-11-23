namespace AzrngTools.Models.Word;

/// <summary>
/// Word 转 Markdown 转换结果：输出路径、统计信息与预览用源文本。
/// </summary>
/// <param name="OutputFilePath">输出的 .md 文件绝对路径。</param>
/// <param name="HeadingCount">识别出的 Markdown 标题行数。</param>
/// <param name="ExportedImageCount">导出到 assets 目录的图片数（忽略图片模式为 0）。</param>
/// <param name="AssetsDirectoryName">图片资源目录名（忽略图片模式为空字符串）。</param>
/// <param name="Markdown">转换后的 Markdown 源文本，供页面预览。</param>
public sealed record WordMarkdownConvertResult(
    string OutputFilePath,
    int HeadingCount,
    int ExportedImageCount,
    string AssetsDirectoryName,
    string Markdown);
