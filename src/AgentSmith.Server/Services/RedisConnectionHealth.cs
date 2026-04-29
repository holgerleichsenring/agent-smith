using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services;

/// <summary>
/// Subscribes to IConnectionMultiplexer connection events and mirrors them into a SubsystemHealth
/// instance. Used by ServerCommand sub-tasks to decide when to start/pause their work, and by the
/// /health endpoint to report 'redis' status.
/// </summary>
public sealed class RedisConnectionHealth
{
    private readonly SubsystemHealth _health;
    private readonly ILogger<RedisConnectionHealth> _logger;

    public RedisConnectionHealth(
        IConnectionMultiplexer multiplexer,
        ILogger<RedisConnectionHealth> logger)
    {
        _health = new SubsystemHealth("redis");
        _logger = logger;

        multiplexer.ConnectionFailed += OnFailed;
        multiplexer.ConnectionRestored += OnRestored;

        if (multiplexer.IsConnected) _health.SetUp();
        else _health.SetDegraded("connecting");
    }

    public ISubsystemHealth Health => _health;

    private void OnFailed(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogWarning("Redis connection lost: {FailureType} {Message}",
            e.FailureType, e.Exception?.Message ?? "unknown");
        _health.SetDegraded($"{e.FailureType}: {e.Exception?.Message ?? "unknown"}");
    }

    private void OnRestored(object? sender, ConnectionFailedEventArgs e)
    {
        _logger.LogInformation("Redis connection restored");
        _health.SetUp();
    }
}
