namespace AzrngTools.Models.Format;

/// <summary>
/// 格式校验结果
/// </summary>
public sealed record ConversionCheckResult(bool IsSuccess, string Message)
{
    public static ConversionCheckResult Success(string message) => new(true, message);

    public static ConversionCheckResult Failure(string message) => new(false, message);
}
