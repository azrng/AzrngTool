using Microsoft.Extensions.DependencyInjection;
using AzrngTools.Services.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Tests.Services.Database;

public class DatabaseDependencyInjectionTests
{
    [Fact]
    public void Database_workbench_services_resolve_from_dependency_injection()
    {
        var services = new ServiceCollection();
        services.RegisterBusinessServices(typeof(DatabaseService).Assembly);
        services.AddTransient<MainWindowViewModel>();

        using var provider = services.BuildServiceProvider();

        var databaseService = provider.GetRequiredService<IDatabaseService>();
        var viewModel = provider.GetRequiredService<MainWindowViewModel>();

        Assert.Same(databaseService, provider.GetRequiredService<IDatabaseService>());
        Assert.NotNull(viewModel.BrowserViewModel);
        Assert.NotNull(viewModel.TableDetailViewModel);
        Assert.NotNull(viewModel.ViewDetailViewModel);
        Assert.NotNull(viewModel.StoredProcedureDetailViewModel);
        Assert.NotNull(viewModel.SqlQueryViewModel);
    }
}
