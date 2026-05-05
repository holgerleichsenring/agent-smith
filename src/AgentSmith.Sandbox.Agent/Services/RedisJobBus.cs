using System.Text.Json;
using AgentSmith.Sandbox.Agent.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class RedisJobBus : IRedisJobBus
{
    public const int ConnectRetryCount = 5;
    private static readonly TimeSpan StepPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _database;
    private readonly RedisEventChannel _eventChannel;
    private readonly bool _ownsMultiplexer;

    private RedisJobBus(IConnectionMultiplexer multiplexer, ILogger logger, bool ownsMultiplexer)
    {
        _multiplexer = multiplexer;
        _database = multiplexer.GetDatabase();
        _eventChannel = new RedisEventChannel(_database, logger);
        _ownsMultiplexer = ownsMultiplexer;
    }

    public static async Task<RedisJobBus> ConnectAsync(string redisUrl, ILogger logger, CancellationToken cancellationToken)
    {
        var options = ConfigurationOptions.Parse(redisUrl);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = ConnectRetryCount;
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
        return new RedisJobBus(multiplexer, logger, ownsMultiplexer: true);
    }

    internal static RedisJobBus FromMultiplexer(IConnectionMultiplexer multiplexer, ILogger logger) =>
        new(multiplexer, logger, ownsMultiplexer: false);

    public async Task<Step?> WaitForStepAsync(string jobId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        var key = RedisKeys.InputKey(jobId);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await _database.ListRightPopAsync(key);
            if (!value.IsNull)
            {
                return JsonSerializer.Deserialize<Step>(value.ToString(), WireFormat.Json);
            }
            await Task.Delay(StepPollInterval, cancellationToken);
        }
        return null;
    }

    public void EnqueueEventsBatch(string jobId, IReadOnlyList<StepEvent> events) =>
        _eventChannel.TryEnqueue(jobId, events);

    public async Task PushResultAsync(string jobId, StepResult result, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(result, WireFormat.Json);
        await _database.ListLeftPushAsync(RedisKeys.ResultsKey(jobId), json);
    }

    public async ValueTask DisposeAsync()
    {
        await _eventChannel.DisposeAsync();
        if (_ownsMultiplexer)
        {
            await _multiplexer.CloseAsync();
            _multiplexer.Dispose();
        }
    }
}
