using Azrng.Core.Results;

namespace AzrngTools.Services.Word;

internal static class WordResultModel
{
    private const string FailureCode = "WORD_ERROR";

    public static IResultModel<T> Success<T>(T data, string message)
    {
        return ResultModelFactory.Success(data, message);
    }

    public static IResultModel<T> Failure<T>(string message)
    {
        return ResultModelFactory.Failure<T>(message, FailureCode);
    }
}
