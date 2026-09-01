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
        // 2026-08-28-b630: the declaration is decidable from config alone, so it is read
        // before anything that touches a sandbox; the presence assertion needs the sandbox
        // that carries the value and therefore follows the probe that proves it usable.
        services.AddTransient<IRunPreflightCheck, DeclaredSecretCheck>();
        services.AddTransient<IRunPreflightCheck, SandboxHomeWritableCheck>();
        services.AddSingleton<Sandbox.ISandboxSecretPresenceProbe, Sandbox.SandboxSecretPresenceProbe>();
        services.AddTransient<IRunPreflightCheck, InjectedSecretCheck>();
        services.AddTransient<IRunPreflightCheck, BranchStateCheck>();
        return services;
    }
}
