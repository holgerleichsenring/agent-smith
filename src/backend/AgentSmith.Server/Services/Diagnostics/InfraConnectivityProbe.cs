using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// Live reachability probes for Redis + the persistence DB. Redis pings via the
/// shared multiplexer; the scoped <see cref="AgentSmithDbContext"/> is resolved
/// per-probe through <see cref="IServiceScopeFactory"/> (the diagnostics service
/// is a singleton and must not capture a scoped DbContext). Never throws.
/// </summary>
internal sealed class InfraConnectivityProbe(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    ILogger<InfraConnectivityProbe> logger) : IInfraConnectivityProbe
{
    public async Task<ConnectionProbeResult> ProbeRedisAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await redis.GetDatabase().PingAsync();
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Redis probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<ConnectionProbeResult> ProbePersistenceAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>();
            var reachable = await db.Database.CanConnectAsync(cancellationToken);
            return reachable
                ? ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds)
                : ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, "database not reachable");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Persistence probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
