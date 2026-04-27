using AgentSmith.Application.Services.Health;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Wraps a leader-elected subsystem (housekeeping, poller) with health-state tracking and the
/// redis-gated retry loop. Collapses the identical plumbing in ServerCommand for the two
/// LeaderElectedHostedService consumers (p0101).
/// </summary>
internal static class LeaderSubsystemRunner
{
    public static Task RunAsync(
        IServiceProvider provider,
        SubsystemHealth health,
        string leaseKey,
        Func<CancellationToken, Task> work,
        int redisRetryIntervalSeconds,
        CancellationToken cancellationToken)
    {
        var logger = provider.GetRequiredService<ILogger<LeaderElectedHostedService>>();
        return SubsystemTask.RunRedisGatedAsync<IRedisLeaderLease>(
            provider, health, redisRetryIntervalSeconds,
            (lease, ct) => new LeaderElectedHostedService(leaseKey, work, lease, logger).RunAsync(ct),
            logger, cancellationToken);
    }
}
