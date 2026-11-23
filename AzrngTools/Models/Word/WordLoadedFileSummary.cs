namespace AzrngTools.Models.Word;

/// <summary>
/// 载入的 Word 文件摘要，供页面展示。
/// </summary>
/// <param name="FilePath">文件绝对路径。</param>
/// <param name="FileName">文件名（含扩展名）。</param>
/// <param name="FileSizeBytes">文件大小（字节）。</param>
public sealed record WordLoadedFileSummary(
    string FilePath,
    string FileName,
    long FileSizeBytes);
