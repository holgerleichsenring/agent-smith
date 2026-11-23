using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-22-747db: the one way a product worker is registered — behind <see cref="WorkerLauncher{T}"/>, so nothing a
/// worker does or depends on runs before the server listens. HostedServiceRegistrationRuleTests fails any other.
/// </summary>
public static class WorkerExtensions
{
    public static IServiceCollection AddWorker<T>(this IServiceCollection services) where T : class, IHostedService
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, WorkerLauncher<T>>());
        return services;
    }
}
