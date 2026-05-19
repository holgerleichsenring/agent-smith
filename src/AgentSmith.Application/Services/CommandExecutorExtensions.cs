using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services;

/// <summary>
/// Generic command dispatcher used by every pipeline step. Transient (not Singleton):
/// a Singleton CommandExecutor would receive the ROOT IServiceProvider regardless of
/// where it's resolved from, so its GetService&lt;ICommandHandler&lt;T&gt;&gt;() calls
/// would resolve handlers from the root scope. Handlers like GeneratePlanHandler depend
/// on Scoped services (IAgentProviderFactory), so this trips ValidateScopes at runtime.
/// Transient means CommandExecutor injects the same provider its caller was resolved
/// from — Server flows resolve through a request scope, CLI flows resolve from root
/// and have no Scoped deps that matter (no DialogueTrail).
/// </summary>
public static class CommandExecutorExtensions
{
    public static IServiceCollection AddCommandExecutor(this IServiceCollection services)
    {
        services.AddTransient<ICommandExecutor, CommandExecutor>();
        return services;
    }
}
