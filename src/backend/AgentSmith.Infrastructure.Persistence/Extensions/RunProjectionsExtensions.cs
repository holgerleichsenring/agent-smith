using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>
/// p0403/p0404: the run-event applier's PROJECTIONS — one service per thing a run
/// event changes (checkpoint, ratified expectation, capacity queue, sandbox
/// lifetime, per-step time). Registered together because they are one seam: the
/// applier routes an event, a projection owns what it means. Services the
/// composition root can see, never statics the applier reaches for.
/// </summary>
public static class RunProjectionsExtensions
{
    public static IServiceCollection AddRunProjections(this IServiceCollection services)
    {
        services.AddSingleton<RunCheckpointProjection>();
        services.AddSingleton<RunExpectationProjection>();
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunSandboxProjection>();
        services.AddSingleton<RunStepTimeProjection>();
        services.AddSingleton<RunPullRequestProjection>();
        // p0413: what the scope classifier decided about the ticket — its size and
        // its shape — on the run row.
        services.AddSingleton<RunClassificationProjection>();
        // p0466: the run's terminal transition, and the phase as a thing of its own.
        services.AddSingleton<RunFinalizationProjection>();
        // 2026-08-24-ca23: ending a run whose terminal event nobody drains, and the unfinished
        // runs a cold start must anchor because a pause removed them from the active set.
        services.AddSingleton<RunTrailBuffers>();
        services.AddSingleton<CancelTerminalWriter>();
        services.AddSingleton<IUnfinishedRunSource, UnfinishedRunSource>();
        services.AddSingleton<RunPhaseProjection>();
        // 2026-08-25-61f1: the three tables a run event INSERTS into own their own rows —
        // and with them the rule that one event's row is written once.
        services.AddSingleton<ProjectedEventRecords>();
        services.AddSingleton<RunMetricsProjection>();
        services.AddSingleton<RunStepProjection>();
        services.AddSingleton<RunLlmCallProjection>();
        services.AddSingleton<RunDecisionProjection>();
        return services;
    }
}
