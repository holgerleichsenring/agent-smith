using AgentSmith.Application.Services.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Cli.Services;

/// <summary>
/// Wraps a Redis-dependent server task with health-state tracking and a wait-for-redis
/// retry loop. If TService is unregistered, the subsystem transitions to Disabled and
/// the task returns. If TService is registered but the multiplexer is disconnected, the
/// task waits in Degraded until the multiplexer connects, then runs the inner work.
/// On task error, transitions Degraded and re-enters the wait loop.
/// </summary>
public static class SubsystemTask
{
    public static async Task RunRedisGatedAsync<TService>(
        IServiceProvider provider,
        SubsystemHealth health,
        int retryIntervalSeconds,
        Func<TService, CancellationToken, Task> work,
        ILogger logger,
        CancellationToken cancellationToken)
        where TService : class
    {
        var service = provider.GetService<TService>();
        if (service is null)
        {
            health.SetDisabled("REDIS_URL not configured");
            logger.LogInformation(
                "{Subsystem} disabled: REDIS_URL not configured", health.Name);
            return;
        }

        var multiplexer = provider.GetRequiredService<IConnectionMultiplexer>();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!await WaitUntilConnectedAsync(
                    health, multiplexer, retryIntervalSeconds, logger, cancellationToken))
                return;

            await RunOnceAsync(service, work, health, logger, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
        }
    }

    private static async Task<bool> WaitUntilConnectedAsync(
        SubsystemHealth health, IConnectionMultiplexer multiplexer,
        int retryIntervalSeconds, ILogger logger, CancellationToken ct)
    {
        if (multiplexer.IsConnected) return true;

        health.SetDegraded("waiting for Redis");
        logger.LogInformation(
            "{Subsystem} waiting for Redis connection (retry every {Interval}s)",
            health.Name, retryIntervalSeconds);

        while (!ct.IsCancellationRequested && !multiplexer.IsConnected)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(retryIntervalSeconds), ct); }
            catch (OperationCanceledException) { return false; }
        }
        return !ct.IsCancellationRequested;
    }

    private static async Task RunOnceAsync<TService>(
        TService service,
        Func<TService, CancellationToken, Task> work,
        SubsystemHealth health,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            health.SetUp();
            logger.LogInformation("{Subsystem} started", health.Name);
            await work(service, ct);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            health.SetDegraded($"task error: {ex.Message}");
            logger.LogWarning(ex,
                "{Subsystem} task ended with error — entering retry loop", health.Name);
        }
    }
}
