namespace AzrngTools.Models.Network;

public static class ApiRequestBodyModes
{
    public const string None = "None";
    public const string FormData = "FormData";
    public const string FormUrlEncoded = "FormUrlEncoded";
    public const string RawJson = "RawJson";
    public const string RawXml = "RawXml";
    public const string RawText = "RawText";
}

public sealed class ApiRequestKeyValueItem
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class ApiRequestSnapshot
{
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "https://";
    public string BodyMode { get; set; } = ApiRequestBodyModes.None;
    public string BodyContent { get; set; } = string.Empty;
    public bool IgnoreSslErrors { get; set; }
    public List<ApiRequestKeyValueItem> QueryParameters { get; set; } = [];
    public List<ApiRequestKeyValueItem> Headers { get; set; } = [];
    public List<ApiRequestKeyValueItem> FormFields { get; set; } = [];
}

public sealed class ApiResponseHeader
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class ApiResponseSnapshot
{
    public int? StatusCode { get; set; }
    public long DurationMs { get; set; }
    public long SizeBytes { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string FinalUrl { get; set; } = string.Empty;
    public string RequestSummary { get; set; } = string.Empty;
    public List<ApiResponseHeader> Headers { get; set; } = [];
}

public sealed class ApiRequestExecutionResult
{
    public bool IsSuccess { get; init; }
    public bool IsCanceled { get; init; }
    public string Message { get; init; } = string.Empty;
    public ApiResponseSnapshot? Response { get; init; }

    public static ApiRequestExecutionResult Success(ApiResponseSnapshot response, string message = "请求发送完成。")
    {
        return new ApiRequestExecutionResult
        {
            IsSuccess = true,
            Message = message,
            Response = response
        };
    }

    public static ApiRequestExecutionResult Failure(string message, ApiResponseSnapshot? response = null)
    {
        return new ApiRequestExecutionResult
        {
            IsSuccess = false,
            Message = message,
            Response = response
        };
    }

    public static ApiRequestExecutionResult Canceled(string message, ApiResponseSnapshot? response = null)
    {
        return new ApiRequestExecutionResult
        {
            IsSuccess = false,
            IsCanceled = true,
            Message = message,
            Response = response
        };
    }
}

public sealed class ApiRequestHistoryItem
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public ApiRequestSnapshot Request { get; set; } = new();
    public ApiResponseSnapshot? Response { get; set; }
}

public sealed class ApiRequestToolStore
{
    public List<ApiRequestHistoryItem> HistoryItems { get; set; } = [];
}
