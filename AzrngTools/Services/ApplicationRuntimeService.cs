using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace AzrngTools.Services;

public sealed class ApplicationRuntimeService : IApplicationRuntimeService, ISingletonDependency
{
    public void Shutdown()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
            return;
        }

        Environment.Exit(0);
    }
}
