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

        try
        {
            finalUrl = BuildUrl(request);
            using var client = CreateHttpClient(request.IgnoreSslErrors);
            using var message = new HttpRequestMessage(new HttpMethod(request.Method), finalUrl);

            message.Content = BuildContent(request);
            ApplyHeaders(message, request.Headers);

            var stopwatch = Stopwatch.StartNew();
            using var response = await client.SendAsync(message, cancellationToken);
            stopwatch.Stop();

            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            // 响应体读取与 JSON 美化对大响应是秒级重活，移到线程池执行，避免回 UI 线程造成窗口冻结
            var responseSnapshot = await Task.Run(async () =>
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ApiResponseSnapshot
                {
                    StatusCode = (int)response.StatusCode,
                    DurationMs = elapsedMilliseconds,
                    SizeBytes = response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(content),
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
            LocalLogHelper.LogError($"HTTP 请求失败: {request.Method} {finalUrl}\n{exception.GetExceptionAndStack()}");
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
            LocalLogHelper.LogError($"JSON 格式化失败: {ex.Message}");
            return content;
        }
    }
}
