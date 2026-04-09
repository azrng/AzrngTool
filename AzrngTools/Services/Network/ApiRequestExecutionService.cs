using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AzrngTools.Models.Network;

namespace AzrngTools.Services.Network;

public sealed class ApiRequestExecutionService : IApiRequestExecutionService, ITransientDependency
{
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

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseSnapshot = new ApiResponseSnapshot
            {
                StatusCode = (int)response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                SizeBytes = Encoding.UTF8.GetByteCount(content),
                Content = TryFormatJson(content),
                Headers = response.Headers.Concat(response.Content.Headers)
                    .SelectMany(header => header.Value.Select(value => new ApiResponseHeader
                    {
                        Name = header.Key,
                        Value = value
                    }))
                    .ToList(),
                RequestSummary = $"{request.Method.ToUpperInvariant()} {finalUrl}"
            };

            return ApiRequestExecutionResult.Success(responseSnapshot);
        }
        catch (Exception exception)
        {
            var failureResponse = new ApiResponseSnapshot
            {
                Content = $"请求发送失败：{exception.Message}",
                ErrorMessage = exception.Message,
                RequestSummary = string.IsNullOrWhiteSpace(finalUrl)
                    ? request.Url
                    : $"{request.Method.ToUpperInvariant()} {finalUrl}"
            };

            return ApiRequestExecutionResult.Failure($"请求发送失败：{exception.Message}", failureResponse);
        }
    }

    private HttpClient CreateHttpClient(bool ignoreSslErrors)
    {
        if (!ignoreSslErrors)
        {
            return _httpClientFactory.CreateClient();
        }

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        return new HttpClient(handler, disposeHandler: true);
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
        var url = request.Url;

        foreach (var pathParameter in request.PathParameters.Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            var value = Uri.EscapeDataString(pathParameter.Value);
            url = url.Replace($"{{{pathParameter.Name}}}", value, StringComparison.OrdinalIgnoreCase);
            url = url.Replace($":{pathParameter.Name}", value, StringComparison.OrdinalIgnoreCase);
        }

        var builder = new UriBuilder(url);
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

    private static string TryFormatJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return content;
        }
    }
}
