namespace AzrngTools.Models;

public sealed class AppUpdateApplyResult
{
    public required bool IsSuccess { get; init; }

    public required string Message { get; init; }
}
