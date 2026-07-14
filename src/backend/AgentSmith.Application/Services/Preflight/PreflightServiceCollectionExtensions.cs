using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Preflight;

/// <summary>
/// p0324: registers the preflight runner plus every composition-neutral check, in
/// the order they run and print. The composition root must additionally register the
/// backend-specific probe seams: <see cref="IPreflightSandboxProbe"/> and
/// <see cref="IPreflightInfraProbe"/> (CLI: round-trip + env probes; server: job
/// spawner + shared multiplexer/DbContext probes).
/// </summary>
public static class PreflightServiceCollectionExtensions
{
    public static IServiceCollection AddPreflight(this IServiceCollection services)
    {
        services.AddSingleton<IPreflightRunner, PreflightRunner>();
        services.AddSingleton<IPreflightConfigSource, PreflightConfigSource>();
        services.AddSingleton<IPreflightCheck, ConfigSchemaCheck>();
        services.AddSingleton<IPreflightCheck, LlmReachableCheck>();
        services.AddSingleton<IPreflightCheck>(sp => new TrackerAuthCheck(
            sp.GetRequiredService<IPreflightConfigSource>(),
            sp.GetRequiredService<Contracts.Providers.ITicketProviderFactory>(),
            Environment.GetEnvironmentVariable));
        services.AddSingleton<IPreflightCheck, RepoAccessCheck>();
        services.AddSingleton<IPreflightCheck, SkillsCatalogCheck>();
        services.AddSingleton<IPreflightCheck, SandboxSpawnCheck>();
        services.AddSingleton<IPreflightCheck, InfraCheck>();
        return services;
    }
}
