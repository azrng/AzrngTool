namespace AzrngTools.Services;

public interface IAppInfoService
{
    string Version { get; }

    string InformationalVersion { get; }

    string RepositoryOwner { get; }

    string RepositoryName { get; }

    string RepositoryUrl { get; }

    string UpdateAssetName { get; }

    string BaseDirectory { get; }

    string ExecutablePath { get; }
}
