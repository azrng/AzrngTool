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
        // 一次程序集扫描完成三种生命周期注册，避免 GetTypes() 全量反射在启动时重复执行三次
        var lifetimeMarkers = new (Type MarkerType, ServiceLifetime Lifetime)[]
        {
            (typeof(ITransientDependency), ServiceLifetime.Transient),
            (typeof(IScopedDependency), ServiceLifetime.Scoped),
            (typeof(ISingletonDependency), ServiceLifetime.Singleton),
        };

        //不自动注册该命名空间下的接口
        var ignoreNameSpaces = new[]
        {
            "Microsoft.",
            "System."
        };

        var implementationTypes = assemblies
                                  .SelectMany(a => a.GetTypes())
                                  .Where(t => t.IsClass);

        foreach (var implementType in implementationTypes)
        {
            var interfaces = implementType.GetInterfaces().ToList();
            interfaces.RemoveAll(x =>
                ignoreNameSpaces.Any(p => x.FullName is not null && x.FullName.IndexOf(p, StringComparison.Ordinal) == 0));
            if (interfaces.Count == 0)
            {
                continue;
            }

            foreach (var (markerType, lifetime) in lifetimeMarkers)
            {
                if (!interfaces.Contains(markerType))
                {
                    continue;
                }

                foreach (var serviceType in interfaces)
                {
                    services.Add(new ServiceDescriptor(serviceType, implementType, lifetime));
                }
            }
        }

        return services;
    }

    #endregion
}