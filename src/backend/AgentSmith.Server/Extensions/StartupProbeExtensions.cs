using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0391a: the startup probes and the runner that publishes what they find. Registered
/// unconditionally — the diagnostic surface is the one part of the server that has no
/// optional dependencies, because it is what reports on all the others.
/// </summary>
internal static class StartupProbeExtensions
{
    internal static IServiceCollection AddStartupProbes(this IServiceCollection services)
    {
        services.AddSingleton<IStartupProbe, ConfigFileProbe>();
        services.AddSingleton<IStartupProbe, PersistenceProbe>();
        services.AddSingleton<IStartupProbe, RedisProbe>();
        services.AddSingleton<IStartupProbe, SpawnerProbe>();
        // Last two: they resolve the loaded configuration, which the probes above explain
        // the state of when it comes back empty.
        services.AddSingleton<IStartupProbe, ConfigurationProbe>();
        services.AddSingleton<IStartupProbe, PinnedAgentProbe>();
        services.AddSingleton<IStartupAnnouncer, StartupAnnouncer>();
        services.AddSingleton<IStartupProbeRunner, StartupProbeRunner>();
        return services;
    }
}
