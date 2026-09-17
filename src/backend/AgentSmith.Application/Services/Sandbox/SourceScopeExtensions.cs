using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Registers the read-only source scopes: how one is spawned and prepared, the factory that
/// builds them, and the ambient observer a caller may set to hear them open.
/// </summary>
public static class SourceScopeExtensions
{
    public static IServiceCollection AddSourceScopes(this IServiceCollection services)
    {
        services.AddTransient<SourceScopeMaterialiser>();
        services.AddTransient<SourceScopeOpener>();
        services.AddTransient<ISourceScopeSandboxFactory, SourceScopeSandboxFactory>();
        // One instance per process is right: the observer lives in the async flow, not here.
        services.TryAddSingleton<ISourceScopeObserverAccessor, AsyncLocalSourceScopeObserverAccessor>();
        return services;
    }
}
