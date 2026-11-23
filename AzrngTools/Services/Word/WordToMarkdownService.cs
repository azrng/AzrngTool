using Azrng.Core.Results;
using AzrngTools.Models.Word;
using AzrngTools.Utils;
using DocSharp.Docx;

namespace AzrngTools.Services.Word;

/// <summary>
/// Word 转 Markdown 转换内核封装：校验、DocSharp 调用、图片目录与相对链接配置、输出落盘与错误包装。
/// </summary>
public sealed class WordToMarkdownService : IWordToMarkdownService, ITransientDependency
{
    public Task<IResultModel<WordLoadedFileSummary>> LoadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationError = ValidateSourceFile(filePath);
                if (validationError is not null)
                {
                    return WordResultModel.Failure<WordLoadedFileSummary>(validationError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(filePath);
                var summary = new WordLoadedFileSummary(filePath, fileInfo.Name, fileInfo.Length);
                return WordResultModel.Success(summary, $"已加载 Word 文件：{fileInfo.Name}");
            }
            catch (OperationCanceledException)
            {
                return WordResultModel.Failure<WordLoadedFileSummary>("Word 文件加载已取消");
            }
            catch (Exception ex)
            {
                var friendlyMessage = BuildFriendlyError("Word 文件加载失败", ex);
                AppLog.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return WordResultModel.Failure<WordLoadedFileSummary>(friendlyMessage);
            }
        }, cancellationToken);
    }

    public Task<IResultModel<WordMarkdownConvertResult>> ConvertAsync(
        WordMarkdownConvertRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var validationError = ValidateConvertRequest(request);
                if (validationError is not null)
                {
                    return WordResultModel.Failure<WordMarkdownConvertResult>(validationError);
                }

                cancellationToken.ThrowIfCancellationRequested();
                using var convertPerf = PerfLogScope.Start(
                    "Word转Markdown",
                    $"{Path.GetFileName(request.SourceFilePath)} {PerfLogScope.DescribeSize(new FileInfo(request.SourceFilePath).Length)}");

                var assetsDirectoryName = $"{Path.GetFileNameWithoutExtension(request.OutputFilePath)}.assets";
                var markdown = ConvertToMarkdown(request, assetsDirectoryName);
                markdown = WordMarkdownPostProcessor.Clean(markdown);

                EnsureOutputDirectory(request.OutputFilePath);
                File.WriteAllText(request.OutputFilePath, markdown);

                // DocSharp 只在文档确实含图时才创建 assets 目录，统计前先确认目录存在
                var assetsPath = Path.Combine(
                    Path.GetDirectoryName(request.OutputFilePath) ?? string.Empty,
                    assetsDirectoryName);
                var exportedImageCount = request.ExportImages && Directory.Exists(assetsPath)
                    ? Directory.EnumerateFiles(assetsPath).Count()
                    : 0;

                var result = new WordMarkdownConvertResult(
                    request.OutputFilePath,
                    CountHeadings(markdown),
                    exportedImageCount,
                    request.ExportImages ? assetsDirectoryName : string.Empty,
                    markdown);
                return WordResultModel.Success(result, $"Markdown 导出完成：{request.OutputFilePath}");
            }
            catch (OperationCanceledException)
            {
                return WordResultModel.Failure<WordMarkdownConvertResult>("Markdown 转换已取消");
            }
            catch (Exception ex)
            {
                var friendlyMessage = BuildFriendlyError("Markdown 转换失败", ex);
                AppLog.LogError($"{friendlyMessage}\n{ex.GetExceptionAndStack()}");
                return WordResultModel.Failure<WordMarkdownConvertResult>(friendlyMessage);
            }
        }, cancellationToken);
    }

    private static string ConvertToMarkdown(WordMarkdownConvertRequest request, string assetsDirectoryName)
    {
        var converter = new DocxToMarkdownConverter
        {
            ExportHeaderFooter = false,
        };

        if (request.ExportImages)
        {
            // 相对前缀必须显式指定：缺省时 DocSharp 写 file:/// 绝对链接，md 拷走即断链
            converter.ImagesOutputFolder = Path.Combine(
                Path.GetDirectoryName(request.OutputFilePath) ?? string.Empty,
                assetsDirectoryName);
            converter.ImagesBaseUriOverride = $"{assetsDirectoryName}/";
        }

        var outputStream = new MemoryStream();
        using (var inputStream = File.OpenRead(request.SourceFilePath))
        {
            DocSharp.Docx.DocxToTextExtensions.Convert(converter, inputStream, outputStream);
        }

        outputStream.Position = 0;
        using var reader = new StreamReader(outputStream);
        return reader.ReadToEnd();
    }

    private static int CountHeadings(string markdown)
    {
        return markdown.Split('\n').Count(line => line.StartsWith("#"));
    }

    private static string? ValidateSourceFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "请选择 Word 文件";
        }

        if (!File.Exists(filePath))
        {
            return "Word 文件不存在或已被移动";
        }

        if (!string.Equals(Path.GetExtension(filePath), ".docx", StringComparison.OrdinalIgnoreCase))
        {
            return "仅支持 .docx 文件（旧版 .doc 请先在 Word 中另存为 .docx）";
        }

        return null;
    }

    private static string? ValidateConvertRequest(WordMarkdownConvertRequest request)
    {
        var sourceError = ValidateSourceFile(request.SourceFilePath);
        if (sourceError is not null)
        {
            return sourceError;
        }

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return "请选择 Markdown 输出文件";
        }

        if (!string.Equals(Path.GetExtension(request.OutputFilePath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            return "Markdown 输出文件扩展名必须为 .md";
        }

        return null;
    }

    private static void EnsureOutputDirectory(string outputFilePath)
    {
        var directory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string BuildFriendlyError(string prefix, Exception exception)
    {
        if (exception is UnauthorizedAccessException)
        {
            return $"{prefix}：没有访问文件或目录的权限";
        }

        if (exception is IOException)
        {
            return $"{prefix}：文件读写失败，请确认文件未被占用且路径有效";
        }

        if (exception is InvalidOperationException)
        {
            return $"{prefix}：{exception.Message}";
        }

        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return $"{prefix}：处理失败，可能是 Word 文档格式不兼容或文件损坏";
        }

        return $"{prefix}：{message}";
    }
}
