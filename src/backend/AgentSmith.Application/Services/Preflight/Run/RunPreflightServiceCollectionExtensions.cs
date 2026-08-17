using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// p0428: the precondition gate and the checks it runs, in the order they are reported.
/// Cheapest and most explanatory first: an empty configuration is the cause of half the
/// findings below it, so it should be the first line the operator reads.
/// </summary>
public static class RunPreflightServiceCollectionExtensions
{
    public static IServiceCollection AddRunPreflight(this IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<RunPreflightContext>, RunPreflightHandler>();
        services.AddTransient<IRunPreflightCheck, ConfiguredAgentCheck>();
        services.AddTransient<IRunPreflightCheck, RegistryCredentialCheck>();
        services.AddTransient<IRunPreflightCheck, SandboxHomeWritableCheck>();
        services.AddTransient<IRunPreflightCheck, BranchStateCheck>();
        return services;
    }
}
