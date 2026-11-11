using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AzrngTools.Models.Network;
using AzrngTools.Utils;

namespace AzrngTools.Services.Network;

public sealed class ApiRequestExecutionService : IApiRequestExecutionService, ITransientDependency
{
    // 忽略 SSL 校验的命名客户端，Handler 配置在应用启动时统一注册
    public const string IgnoreSslClientName = "IgnoreSsl";

    private readonly IHttpClientFactory _httpClientFactory;

    public ApiRequestExecutionService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ApiRequestExecutionResult> SendAsync(ApiRequestSnapshot request, CancellationToken cancellationToken)
    {
        var finalUrl = string.Empty;
        var responseSizeBytes = 0L;

        // 响应规模只有读完响应才知道，上下文延迟到落日志时求值；性能条目不落 URL（可能带 token）
        using var perf = PerfLogScope.Start("API 请求", () => $"{request.Method.ToUpperInvariant()} 响应 {PerfLogScope.DescribeSize(responseSizeBytes)}");

        try
        {
            finalUrl = BuildUrl(request);
            using var client = CreateHttpClient(request.IgnoreSslErrors);
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), finalUrl);

            message.Content = BuildContent(request);
            ApplyHeaders(message, request.Headers);

            var stopwatch = Stopwatch.StartNew();
            // ResponseHeadersRead 避免 HttpClient 在返回响应前把整个响应体读进内存,由下方带上限流式读取接管
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // 响应体读取与 JSON 美化对大响应是秒级重活，移到线程池执行，避免回 UI 线程造成窗口冻结
            var responseSnapshot = await Task.Run(async () =>
            {
                var (content, totalBytes) = await ReadBodyWithCapAsync(response, cancellationToken);
                // ResponseHeadersRead 模式下 SendAsync 返回只代表收到响应头,耗时须读完响应体才结算(原语义为整请求耗时)
                stopwatch.Stop();
                responseSizeBytes = totalBytes;
                return new ApiResponseSnapshot
                {
                    StatusCode = (int)response.StatusCode,
                    DurationMs = stopwatch.ElapsedMilliseconds,
                    SizeBytes = responseSizeBytes,
                    Content = TryFormatJson(content),
                    FinalUrl = finalUrl,
                    RequestSummary = $"{request.Method.ToUpperInvariant()} {finalUrl}",
                    Headers = response.Headers.Concat(response.Content.Headers)
                        .SelectMany(header => header.Value.Select(value => new ApiResponseHeader
                        {
                            Name = header.Key,
                            Value = value
                        }))
                        .ToList()
                };
            }, cancellationToken);

            return ApiRequestExecutionResult.Success(responseSnapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var canceledResponse = new ApiResponseSnapshot
            {
                Content = "请求已取消。",
                ErrorMessage = "请求已取消。",
                FinalUrl = string.IsNullOrWhiteSpace(finalUrl) ? request.Url : finalUrl,
                RequestSummary = $"{request.Method.ToUpperInvariant()} {(string.IsNullOrWhiteSpace(finalUrl) ? request.Url : finalUrl)}"
            };

            return ApiRequestExecutionResult.Canceled("请求已取消。", canceledResponse);
        }
        catch (Exception exception)
        {
            AppLog.LogError($"HTTP 请求失败: {request.Method} {finalUrl}\n{exception.GetExceptionAndStack()}");
            var effectiveUrl = string.IsNullOrWhiteSpace(finalUrl) ? request.Url : finalUrl;
            var failureResponse = new ApiResponseSnapshot
            {
                Content = $"请求发送失败：{exception.Message}",
                ErrorMessage = exception.Message,
                FinalUrl = effectiveUrl,
                RequestSummary = $"{request.Method.ToUpperInvariant()} {effectiveUrl}"
            };

            return ApiRequestExecutionResult.Failure($"请求发送失败：{exception.Message}", failureResponse);
        }
    }

    private HttpClient CreateHttpClient(bool ignoreSslErrors)
    {
        // 忽略 SSL 校验同样走 IHttpClientFactory 命名客户端，避免每次请求重建 Handler 与连接池
        return _httpClientFactory.CreateClient(ignoreSslErrors ? IgnoreSslClientName : string.Empty);
    }

    private static void ApplyHeaders(HttpRequestMessage message, IEnumerable<ApiRequestKeyValueItem> headers)
    {
        foreach (var header in headers.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            if (message.Headers.TryAddWithoutValidation(header.Name, header.Value))
            {
                continue;
            }

            message.Content ??= new ByteArrayContent(Array.Empty<byte>());
            message.Content.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }
    }

    private static HttpContent? BuildContent(ApiRequestSnapshot request)
    {
        return request.BodyMode switch
        {
            ApiRequestBodyModes.None => null,
            ApiRequestBodyModes.RawJson => new StringContent(request.BodyContent, Encoding.UTF8, "application/json"),
            ApiRequestBodyModes.RawXml => new StringContent(request.BodyContent, Encoding.UTF8, "application/xml"),
            ApiRequestBodyModes.RawText => new StringContent(request.BodyContent, Encoding.UTF8, "text/plain"),
            ApiRequestBodyModes.FormUrlEncoded => new FormUrlEncodedContent(BuildFormPairs(request)),
            ApiRequestBodyModes.FormData => BuildMultipartContent(BuildFormPairs(request)),
            _ => null
        };
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildFormPairs(ApiRequestSnapshot request)
    {
        var fields = request.FormFields.Count > 0
            ? request.FormFields
            : ParseKeyValuePairs(request.BodyContent).ToList();

        return fields
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new KeyValuePair<string, string>(item.Name, item.Value))
            .ToList();
    }

    private static MultipartFormDataContent BuildMultipartContent(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var content = new MultipartFormDataContent();
        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        return content;
    }

    public static string BuildUrl(ApiRequestSnapshot request)
    {
        var builder = new UriBuilder(request.Url);
        var queryItems = ParseQuery(builder.Query);
        foreach (var queryParameter in request.QueryParameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            queryItems[queryParameter.Name] = queryParameter.Value;
        }

        builder.Query = string.Join("&", queryItems.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return builder.Uri.ToString();
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cleanQuery = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(cleanQuery))
        {
            return result;
        }

        foreach (var pair in cleanQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }

    public static IReadOnlyList<ApiRequestKeyValueItem> ParseKeyValuePairs(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return [];
        }

        var result = new List<ApiRequestKeyValueItem>();
        foreach (var pair in encoded.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result.Add(new ApiRequestKeyValueItem
            {
                Name = Uri.UnescapeDataString(parts[0]),
                Value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty
            });
        }

        return result;
    }

    // 响应体超过 1MB 时跳过自动 JSON 美化：全量解析加重排的 CPU 与内存代价过高，直接展示原文
    private const int AutoFormatJsonMaxLength = 1024 * 1024;

    // 响应体读取上限(字符)：流式读取达到上限即丢弃剩余流,防止无界大响应(数百 MB)整体进内存;
    // 取 VM 展示截断阈值(1M 字符)的 4 倍,保证 UI 侧"响应体过大已截断"提示语义不变
    private const int MaxResponseBodyChars = 4 * 1024 * 1024;

    /// <summary>
    /// 流式读取响应体,最多读取 <see cref="MaxResponseBodyChars"/> 个 UTF-8 字符,超出部分直接丢弃。
    /// 返回(截断后内容, 字节数)。Content-Length 已知时字节数取响应头;
    /// 否则取已读内容实际字节数(chunked 且超限时为下限)。
    /// </summary>
    private static async Task<(string Content, long SizeBytes)> ReadBodyWithCapAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var builder = new StringBuilder();
        var buffer = new char[64 * 1024];
        while (builder.Length < MaxResponseBodyChars)
        {
            var room = Math.Min(buffer.Length, MaxResponseBodyChars - builder.Length);
            var read = await reader.ReadAsync(buffer.AsMemory(0, room), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        var content = builder.ToString();
        // 截断点可能把 UTF-16 代理对劈开,剥离孤立高代理项避免下游序列化/渲染出现乱码
        if (content.Length > 0 && char.IsHighSurrogate(content[^1]))
        {
            content = content[..^1];
        }

        var sizeBytes = response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(content);
        return (content, sizeBytes);
    }

    private static string TryFormatJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        if (content.Length > AutoFormatJsonMaxLength)
        {
            return content;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return JsonHelper.FormatJsonDocument(document);
        }
        catch (Exception ex)
        {
            AppLog.LogError($"JSON 格式化失败: {ex.Message}");
            return content;
        }
    }
}
