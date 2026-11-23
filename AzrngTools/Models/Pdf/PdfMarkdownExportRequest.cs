namespace AzrngTools.Models.Pdf;

/// <summary>
/// PDF 导出 Markdown 请求：源 PDF 路径、输出 md 路径与图片导出开关。
/// </summary>
/// <param name="SourceFilePath">源 .pdf 文件路径。</param>
/// <param name="OutputFilePath">输出 .md 文件路径。</param>
/// <param name="ExportImages">是否导出图片到 &lt;md 文件名&gt;.assets 目录并使用相对引用；false 时忽略全部图片。</param>
public sealed record PdfMarkdownExportRequest(
    string SourceFilePath,
    string OutputFilePath,
    bool ExportImages);
