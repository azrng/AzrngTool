using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// 服务注册
/// </summary>
public static class ServiceCollectionExtensions
{
    #region 服务注册

    ///  <summary>
    /// 统一服务注册
    ///  </summary>
    ///  <param name="services">服务容器</param>
    ///  <param name="assemblies">需要注册的程序集</param>
    ///  <returns></returns>
    public static IServiceCollection RegisterBusinessServices(this IServiceCollection services,
                                                              params Assembly[] assemblies)
    {
        services.RegisterUniteServices(assemblies, typeof(ITransientDependency), ServiceLifetime.Transient);
        services.RegisterUniteServices(assemblies, typeof(IScopedDependency), ServiceLifetime.Scoped);
        services.RegisterUniteServices(assemblies, typeof(ISingletonDependency), ServiceLifetime.Singleton);

        return services;
    }

    /// <summary>
    ///统一注册服务，注册不同生命周期类型
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies"></param>
    /// <param name="lifeType"></param>
    /// <param name="lifetime"></param>
    /// <returns></returns>
    private static IServiceCollection RegisterUniteServices(this IServiceCollection services,
                                                            IEnumerable<Assembly> assemblies, Type lifeType,
                                                            ServiceLifetime lifetime)
    {
        //不自动注册该命名空间下的接口
        var ignoreNameSpaces = new[]
                               {
                                   "Microsoft.",
                                   "System."
                               };
        var dependencyTypes = assemblies
                              .SelectMany(a => a.GetTypes().Where(t => t.IsClass && t.GetInterfaces().Contains(lifeType)))
                              .ToList();

        dependencyTypes.ForEach(implementType =>
        {
            var interfaces = implementType.GetInterfaces().ToList();
            interfaces.RemoveAll(x =>
                ignoreNameSpaces.Any(p => x.FullName is not null && x.FullName.IndexOf(p, StringComparison.Ordinal) == 0));
            if (interfaces.Count > 0)
            {
                interfaces.ForEach(serviceType =>
                    services.Add(new ServiceDescriptor(serviceType, implementType, lifetime)));
            }
            else
            {
                services.Add(new ServiceDescriptor(implementType, implementType, lifetime));
            }
        });
        return services;
    }

    #endregion
}