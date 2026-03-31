namespace AzrngTools.Services;

public sealed class AppInfoService : IAppInfoService
{
    public string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(AppInfoService).Assembly;
        return assembly.GetName().Version?.ToString() ?? "未知版本";
    }
}
