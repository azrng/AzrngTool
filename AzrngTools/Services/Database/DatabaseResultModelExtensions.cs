using Azrng.Core.Results;

namespace AzrngTools.Services.Database;

internal static class DatabaseResultModelExtensions
{
    public static List<T> DataOrEmpty<T>(this IResultModel<List<T>> result)
    {
        return result.Data ?? [];
    }
}
