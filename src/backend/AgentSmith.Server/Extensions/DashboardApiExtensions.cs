using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.Events.Projection;
using AgentSmith.Server.Services.Tickets;
using Microsoft.AspNetCore.SignalR;
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
        // p0517: the filter that refuses an invocation the caller lacks the permission
        // for. Registered unconditionally and silent while the enforce switch is off, so
        // it READS that switch rather than owning a second copy of it.
        services.AddSingleton<HubPermissionFilter>();
        // 2026-10-15-4a4dc: the hub's project scope, and who an overview nudge and subscription reach.
        services.AddSingleton<RunProjectIndex>().AddSingleton<OverviewAudience>().AddSingleton<HubProjectScope>();
        services.AddTransient(sp => new OverviewSubscription(sp.GetRequiredService<TokenAuthorityConfig>(),
            sp.GetRequiredService<CallerIdentityResolver>(), sp.GetRequiredService<TicketProjects>(), sp.GetRequiredService<JobsBroadcaster>()));
        // 2026-09-17-042ej: and the one that forgets a gone connection's filed-work watch.
        services.AddSingleton<FiledWorkDisconnectFilter>();
        services.AddSignalR(o =>
        {
            // p0367: raise the per-connection parallel-invocation cap. The dashboard opens
            // several concurrent hub invocations on one socket (SubscribeRun + GetTrail +
            // ExpandSandbox); the default of 1 serialised them, so a slow trail read blocked
            // the next subscribe on the same connection and read as a hung connection.
            o.MaximumParallelInvocationsPerClient = 4;
            o.AddFilter<HubPermissionFilter>();
            o.AddFilter<FiledWorkDisconnectFilter>();
        });
        services.AddSingleton<SandboxExpansionRegistry>();
        services.AddSingleton<JobsBroadcaster>();
        services.AddHostedService(sp => sp.GetRequiredService<JobsBroadcaster>());
        services.AddRunEventPipeline();
        services.AddDashboardReaders();
        services.AddTicketReaders(); // 2026-10-13-9a59a: GET /api/tickets
        services.AddDeliveryReads(); // 2026-10-25-0099b: GET /api/delivery
        services.AddRunRetry(); // 2026-10-13-9a59n: POST /api/runs/{runId}/retry
        services.AddTicketDefaults(); // 2026-10-13-8762p: GET/POST /api/tickets/{project}/{ticketId}/defaults
        services.AddProjectInit(); // p0489: POST /api/projects/{name}/init
        services.AddSpecDerivation(); // 2026-10-26-bdd0e: POST /api/tickets/{project}/{ticketId}/derive-specs
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
        services.AddSingleton(sp => new RunDbEventPersistence(sp.GetService<RunDbProjector>()));
        // 2026-10-26-0f19c: only the holder of the projection lease writes; a server without a store has none.
        services.AddSingleton<ProjectionLeadership>().AddSingleton<RunProjectionResumePoint>().AddSingleton<RunProjectionCatchUp>();
        services.AddSingleton(sp => new LeaderGatedRunEventPersistence(sp.GetRequiredService<RunDbEventPersistence>(),
            sp.GetRequiredService<ProjectionLeadership>(), sp.GetRequiredService<RunProjectionCatchUp>()));
        services.AddSingleton<IRunEventPersistence>(sp => sp.GetService<RunDbProjector>() is null
            ? sp.GetRequiredService<RunDbEventPersistence>() : sp.GetRequiredService<LeaderGatedRunEventPersistence>());
        services.AddSingleton<ProjectionLeader>().AddHostedService<ProjectionLeaderHostedService>();
        services.AddSingleton<SandboxDetailEventClassifier>();
        services.AddSingleton<SandboxActivityCoalescer>();
        services.AddSingleton<FiledWorkNudge>();
        // 2026-09-20-9f00: LAST-WINS over the no-op the shared composition bound. This is the
        // only place the hub exists, so it is the only place the real nudge can be built.
        services.AddSingleton<AgentSmith.Contracts.Services.IRunListNudge, JobsHubRunListNudge>();
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
