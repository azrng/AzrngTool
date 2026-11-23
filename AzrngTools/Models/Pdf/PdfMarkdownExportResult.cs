namespace AzrngTools.Models.Pdf;

/// <summary>
/// PDF 导出 Markdown 结果：输出路径与统计信息。
/// </summary>
/// <param name="OutputFilePath">输出的 .md 文件绝对路径。</param>
/// <param name="HeadingCount">识别出的 Markdown 标题行数。</param>
/// <param name="ExportedImageCount">导出到 assets 目录的图片数（忽略图片模式为 0）。</param>
public sealed record PdfMarkdownExportResult(
    string OutputFilePath,
    int HeadingCount,
    int ExportedImageCount);
