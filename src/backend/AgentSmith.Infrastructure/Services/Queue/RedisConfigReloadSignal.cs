using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Queue;

/// <summary>
/// p0353: the config-epoch counter on a single Redis key. INCR on a config write,
/// GET to compare on the leader. Atomic and monotonic, so concurrent imports on
/// different replicas never lose an update — the leader simply rebuilds to the
/// latest value.
/// </summary>
public sealed class RedisConfigReloadSignal(
    IConnectionMultiplexer redis,
    ILogger<RedisConfigReloadSignal> logger) : IConfigReloadSignal
{
    private const string Key = "agentsmith:config:epoch";

    public async Task<long> CurrentEpochAsync(CancellationToken cancellationToken)
    {
        var value = await redis.GetDatabase().StringGetAsync(Key);
        return value.HasValue && value.TryParse(out long epoch) ? epoch : 0;
    }

    public async Task<long> BumpAsync(CancellationToken cancellationToken)
    {
        var epoch = await redis.GetDatabase().StringIncrementAsync(Key);
        logger.LogInformation("Config epoch bumped to {Epoch}", epoch);
        return epoch;
    }
}
