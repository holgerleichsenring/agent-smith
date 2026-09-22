using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Registers the read-only source scopes: how one is spawned and prepared, the factory that
/// builds them, the ambient observer a caller may set to hear them open, and the register
/// that holds one between turns.
/// </summary>
public static class SourceScopeExtensions
{
    public static IServiceCollection AddSourceScopes(this IServiceCollection services)
    {
        services.AddTransient<SourceScopeRefresh>();
        services.AddTransient<SourceScopeMaterialiser>();
        services.AddTransient<SourceScopeOpener>();
        services.AddTransient<ISourceScopeSandboxFactory, SourceScopeSandboxFactory>();
        // One instance per process is right: the observer lives in the async flow, not here.
        services.TryAddSingleton<ISourceScopeObserverAccessor, AsyncLocalSourceScopeObserverAccessor>();
        // 2026-09-22-2d11a: the process-local register of held scopes, and the empty
        // conversation-liveness default — the server composition replaces the reader with
        // the relational one. 2026-09-22-2d11b: a design turn is what fills the register;
        // a composition with no conversations keeps it empty.
        services.TryAddSingleton<IHeldSandboxRegister, HeldSandboxRegister>();
        services.TryAddSingleton<IConversationLivenessReader, NoConversationLivenessReader>();
        // 2026-09-22-2d11b: and the heartbeat a take is verified through. Inert for the
        // same reason: with no Redis there is no heartbeat, so no hold is ever handed out.
        services.TryAddSingleton<ISandboxHeartbeatProbe, NoSandboxHeartbeatProbe>();
        return services;
    }
}
