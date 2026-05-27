using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Containers;

/// <summary>
/// Legacy IContainerRunner used by Dispatcher via DockerJobSpawner. The scanner
/// spawners (Nuclei / Spectral / ZAP) live in their own feature-set extension —
/// this one is just the underlying docker exec wrapper.
/// </summary>
public static class ContainerRunnersExtensions
{
    public static IServiceCollection AddContainerRunners(this IServiceCollection services)
    {
        services.AddSingleton<IContainerRunner, DockerContainerRunner>();
        return services;
    }
}
