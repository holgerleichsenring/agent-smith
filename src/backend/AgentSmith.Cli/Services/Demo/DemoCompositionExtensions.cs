using AgentSmith.Application.Services.Demo;
using AgentSmith.Application.Services.Preflight;
using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Cli.Services.Preflight;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Demo;

/// <summary>
/// p0326: demo-only additions on top of the one-shot CLI graph. The preflight
/// runner is composed from the EXPLICIT demo subset — config-schema,
/// llm-reachable, sandbox-spawn, infra — rather than AddPreflight's full set:
/// the demo needs no tracker, repo remote, or skills pin to be healthy, and a
/// skipped-check wall would only obscure the four that matter here.
/// </summary>
internal static class DemoCompositionExtensions
{
    public static IServiceCollection AddDemo(this IServiceCollection services)
    {
        // p0324 probe seams, CLI flavor: real in-process sandbox round-trip;
        // Redis probed only when REDIS_URL is set (one-shot runs don't use it).
        services.AddSingleton<IPreflightConfigSource, PreflightConfigSource>();
        services.AddSingleton<IPreflightSandboxProbe, SandboxRoundTripProbe>();
        services.AddSingleton<IPreflightInfraProbe, CliInfraPreflightProbe>();
        services.AddSingleton<ConfigSchemaCheck>();
        services.AddSingleton<LlmReachableCheck>();
        services.AddSingleton<SandboxSpawnCheck>();
        services.AddSingleton<InfraCheck>();
        services.AddSingleton<IPreflightRunner>(sp => new PreflightRunner(
            [
                sp.GetRequiredService<ConfigSchemaCheck>(),
                sp.GetRequiredService<LlmReachableCheck>(),
                sp.GetRequiredService<SandboxSpawnCheck>(),
                sp.GetRequiredService<InfraCheck>(),
            ],
            sp.GetRequiredService<ILogger<PreflightRunner>>()));

        services.AddSingleton<IDemoGitInitializer, LocalGitProcessInitializer>();
        services.AddSingleton<DemoWorkspaceMaterializer>();
        services.AddSingleton<DemoResultPresenter>();
        services.AddSingleton<IDemoRunner, DemoRunner>();
        services.AddSingleton<DemoExecutor>();
        return services;
    }
}
