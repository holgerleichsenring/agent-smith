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
        return services;
    }
}
