using Azrng.Core.Results;

namespace AzrngTools.Services.Pdf;

internal static class PdfResultModel
{
    private const string FailureCode = "PDF_ERROR";

    public static IResultModel<T> Success<T>(T data, string message)
    {
        return ResultModelFactory.Success(data, message);
    }

    public static IResultModel<T> Failure<T>(string message)
    {
        return ResultModelFactory.Failure<T>(message, FailureCode);
    }
}
