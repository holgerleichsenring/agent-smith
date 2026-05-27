using AgentSmith.Infrastructure.Core;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Containers;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Output;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Infrastructure.Services.Providers.Tickets;
using AgentSmith.Infrastructure.Services.Security;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

/// <summary>
/// Infrastructure composition root: a flat list of per-feature-set Add calls.
/// Removing one call removes one feature-set; the program still compiles. Each
/// feature-set's AddXxx() lives next to the services it registers.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithInfrastructure(this IServiceCollection services)
    {
        services.AddAgentSmithCore();
        // AddHttpClient() is called once at the composition root (Server's Program.cs
        // / CLI's ServiceProviderFactory). Per-feature extensions use AddHttpClient<T>()
        // typed clients instead of the bare factory registration.
        services.AddTicketProviders();
        services.AddSourceProviders();
        services.AddAgentProviders();
        services.AddOutputStrategies();
        services.AddContainerRunners();
        services.AddSecurityScanners();
        services.AddDialogueTransport();
        services.AddProjectMeta();
        return services;
    }
}
