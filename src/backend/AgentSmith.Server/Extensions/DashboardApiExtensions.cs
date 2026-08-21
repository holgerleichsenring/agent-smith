using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0169a: the dashboard API. Gated by AGENTSMITH_UI_API_ENABLED (default on); operators
/// that ship without the dashboard can flip it off via env-var.
/// </summary>
internal static class DashboardApiExtensions
{
    private const string EnabledEnvVar = "AGENTSMITH_UI_API_ENABLED";

    internal static bool IsEnabled => !string.Equals(
        Environment.GetEnvironmentVariable(EnabledEnvVar), "false",
        StringComparison.OrdinalIgnoreCase);

    internal static IServiceCollection AddDashboardApi(this IServiceCollection services)
    {
        services.AddDashboardCors();
        // p0367: raise the per-connection parallel-invocation cap. The dashboard opens
        // several concurrent hub invocations on one socket (SubscribeRun + GetTrail +
        // ExpandSandbox); the default of 1 serialised them, so a slow trail read blocked
        // the next subscribe on the same connection and read as a hung connection.
        services.AddSignalR(o => o.MaximumParallelInvocationsPerClient = 4);
        services.AddSingleton<SandboxExpansionRegistry>();
        services.AddSingleton<JobsBroadcaster>();
        services.AddHostedService(sp => sp.GetRequiredService<JobsBroadcaster>());
        services.AddRunEventPipeline();
        services.AddDashboardReaders();
        services.AddProjectInit(); // p0489: POST /api/projects/{name}/init
        services.AddDashboardSwagger();
        return services;
    }

    // p0367: fan-out is pure transport (JobsHubFanout), wrapped in a back-pressure guard
    // so one stalled client never blocks the shared drain loop. Persistence is a separate
    // seam (IRunEventPersistence -> the optional RunDbProjector), so tool-call writes stay
    // batched and off the Run-group send. The RunEventRouter decides, per event,
    // meaning-vs-detail routing.
    private static IServiceCollection AddRunEventPipeline(this IServiceCollection services)
    {
        services.AddSingleton<JobsHubFanout>();
        services.AddSingleton<FanoutBackpressureOptions>();
        services.AddSingleton<IRunEventFanout>(sp => new BackpressureSafeFanout(
            sp.GetRequiredService<JobsHubFanout>(),
            sp.GetRequiredService<FanoutBackpressureOptions>(),
            sp.GetRequiredService<ILogger<BackpressureSafeFanout>>()));
        services.AddSingleton<IRunEventPersistence>(sp => new RunDbEventPersistence(
            sp.GetService<RunDbProjector>()));
        services.AddSingleton<SandboxDetailEventClassifier>();
        services.AddSingleton<SandboxActivityCoalescer>();
        services.AddSingleton<RunEventRouter>();
        return services;
    }

    private static IServiceCollection AddDashboardSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new() { Title = "agent-smith", Version = "v1" });
            o.DocInclusionPredicate((_, api) =>
                api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
        });
        return services;
    }
}
