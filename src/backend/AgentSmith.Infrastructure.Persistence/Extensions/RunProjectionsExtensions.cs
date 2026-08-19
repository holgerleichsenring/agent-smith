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
        services.AddSingleton<RunPhaseProjection>();
        return services;
    }
}
