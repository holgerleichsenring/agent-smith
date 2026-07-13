using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
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
/// p0324: also backs the infra preflight check (<see cref="IPreflightInfraProbe"/>)
/// — the server can always probe both stores, and the persistence probe also fails
/// on pending migrations because the server never migrates itself.
/// </summary>
internal sealed class InfraConnectivityProbe(
    IConnectionMultiplexer redis,
    IServiceScopeFactory scopeFactory,
    ILogger<InfraConnectivityProbe> logger) : IInfraConnectivityProbe, IPreflightInfraProbe
{
    public string? RedisUnavailableReason => null;

    public string? PersistenceUnavailableReason => null;

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
            if (!reachable)
                return ConnectionProbeResult.Unreachable(
                    stopwatch.ElapsedMilliseconds, "database not reachable");

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return pending == 0
                ? ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds)
                : ConnectionProbeResult.Unreachable(
                    stopwatch.ElapsedMilliseconds,
                    $"{pending} pending migration(s) — run 'agentsmith database migrate'");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Persistence probe failed");
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }
}
