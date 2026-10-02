using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-02-5f89e: liveness of the runs this server drives, over the relational store —
/// the lease reaper it always had, and the run-row beat with the reaper that reads it.
/// 2026-10-02-5ab2b: and the queued request a lost Redis entry is recovered from.
/// </summary>
internal static class RunLivenessExtensions
{
    internal static IServiceCollection AddRunLiveness(this IServiceCollection services)
    {
        services.AddScoped<RunLivenessRepository>();
        services.RemoveAll<IRunHeartbeat>();
        services.AddSingleton<IRunHeartbeat, DbRunHeartbeat>();
        services.AddSingleton<RunLivenessReaper>();
        services.AddHostedService<ActiveRunReaperHostedService>();
        services.AddHostedService<RunLivenessReaperHostedService>();
        // 2026-10-02-5ab2b: a queued request kept on its row, claimed at the pop, swept when lost.
        services.AddScoped<QueuedRunRepository>();
        services.RemoveAll<IRunStartClaim>();
        services.AddSingleton<IRunStartClaim, DbRunStartClaim>();
        services.AddSingleton<QueuedRunDispatch>().AddSingleton<QueuedRunSweeper>();
        return services;
    }
}
