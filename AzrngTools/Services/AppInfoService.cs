using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AzrngTools.Services;

public sealed class AppInfoService : IAppInfoService
{
    public AppInfoService()
    {
        var assembly = ResolveAppAssembly();
        InformationalVersion = ResolveInformationalVersion(assembly);
        Version = ResolveVersion(assembly, InformationalVersion);
    }

    public string Version { get; }

    public string InformationalVersion { get; }

    public string RepositoryOwner { get; } = "azrng";

    public string RepositoryName { get; } = "AzrngTool";

    public string RepositoryUrl { get; } = "https://github.com/azrng/AzrngTool";

    public string UpdateAssetName { get; } = "AzrngTools-win-x64-portable.zip";

    public string BaseDirectory { get; } = AppContext.BaseDirectory;

    public string ExecutablePath { get; } = ResolveExecutablePath();

    private static Assembly ResolveAppAssembly()
    {
        return Assembly.GetEntryAssembly() ?? typeof(AppInfoService).Assembly;
    }

    private static string ResolveVersion(Assembly assembly, string informationalVersion)
    {
        var comparableVersion = ExtractComparableVersion(informationalVersion);
        if (!string.IsNullOrWhiteSpace(comparableVersion))
        {
            return comparableVersion;
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is null)
        {
            return "未知版本";
        }

        return assemblyVersion.Revision == 0
            ? assemblyVersion.ToString(3)
            : assemblyVersion.ToString();
    }

    private static string ResolveInformationalVersion(Assembly assembly)
    {
        var attribute = assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>()
                                .FirstOrDefault();

        return string.IsNullOrWhiteSpace(attribute?.InformationalVersion)
            ? assembly.GetName().Version?.ToString() ?? "未知版本"
            : attribute.InformationalVersion;
    }

    private static string ExtractComparableVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = Regex.Match(value, @"\d+\.\d+\.\d+(?:\.\d+)?");
        if (!match.Success)
        {
            return string.Empty;
        }

        return NormalizeVersionText(match.Value);
    }

    private static string NormalizeVersionText(string value)
    {
        if (!System.Version.TryParse(value, out var version))
        {
            return value;
        }

        var segmentCount = value.Count(character => character == '.') + 1;
        return segmentCount <= 3 || version.Revision == 0
            ? version.ToString(3)
            : version.ToString();
    }

    private static string ResolveExecutablePath()
    {
        return Environment.ProcessPath
               ?? Process.GetCurrentProcess().MainModule?.FileName
               ?? Path.Combine(AppContext.BaseDirectory, "AzrngTools.exe");
    }
}
