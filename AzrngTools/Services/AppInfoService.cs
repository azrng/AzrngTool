using System.Diagnostics;

namespace AzrngTools.Services;

public sealed class AppInfoService : IAppInfoService
{
    public string Version { get; } = ResolveVersion();

    public string RepositoryOwner { get; } = "azrng";

    public string RepositoryName { get; } = "AzrngTool";

    public string RepositoryUrl { get; } = "https://github.com/azrng/AzrngTool";

    public string UpdateAssetName { get; } = "AzrngTools-win-x64-portable.zip";

    public string BaseDirectory { get; } = AppContext.BaseDirectory;

    public string ExecutablePath { get; } = ResolveExecutablePath();

    private static string ResolveVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly() ?? typeof(AppInfoService).Assembly;
        return assembly.GetName().Version?.ToString() ?? "未知版本";
    }

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
               ?? Process.GetCurrentProcess().MainModule?.FileName
               ?? Path.Combine(AppContext.BaseDirectory, "AzrngTools.exe");
    }
}
