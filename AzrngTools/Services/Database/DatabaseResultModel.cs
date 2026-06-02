using Azrng.Core.Exceptions;
using Azrng.Core.Results;

namespace AzrngTools.Services.Database;

internal static class DatabaseResultModel
{
    private const string FailureCode = "DATABASE_ERROR";

    public static IResultModel<T> Success<T>(T data, string message)
    {
        var result = ResultModel<T>.Success(data);
        if (result is ResultModel<T> concreteResult)
        {
            concreteResult.Message = message;
        }

        return result;
    }

    public static IResultModel<T> Failure<T>(string message, string errorCode = FailureCode)
    {
        return ResultModel<T>.Failure(message, errorCode);
    }

    public static IResultModel<T> Failure<T>(Exception exception, string message)
    {
        var errorCode = exception is BaseException baseException
            ? baseException.ErrorCode
            : FailureCode;

        return Failure<T>(message, errorCode);
    }
}
