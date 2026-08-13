using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0403/p0404: the run-event applier's PROJECTIONS — one service per thing a run
/// event changes (checkpoint, ratified expectation, capacity queue, sandbox
/// lifetime, per-step time). Registered together because they are one seam: the
/// applier routes an event, a projection owns what it means. Services the
/// composition root can see, never statics the applier reaches for.
/// </summary>
internal static class RunProjectionsExtensions
{
    internal static IServiceCollection AddRunProjections(this IServiceCollection services)
    {
        services.AddSingleton<RunCheckpointProjection>();
        services.AddSingleton<RunExpectationProjection>();
        services.AddSingleton<QueuedRunProjection>();
        services.AddSingleton<RunSandboxProjection>();
        services.AddSingleton<RunStepTimeProjection>();
        return services;
    }
}
