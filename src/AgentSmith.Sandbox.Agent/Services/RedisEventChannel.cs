using System.Text.Json;
using System.Threading.Channels;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Sandbox.Agent.Services;

internal sealed class RedisEventChannel : IAsyncDisposable
{
    public const int Capacity = 1000;
    public static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)];

    private readonly IDatabase _database;
    private readonly ILogger _logger;
    private readonly Channel<EventBatch> _channel;
    private readonly Task _consumerTask;

    public RedisEventChannel(IDatabase database, ILogger logger)
    {
        _database = database;
        _logger = logger;
        _channel = Channel.CreateBounded<EventBatch>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _consumerTask = Task.Run(ConsumeAsync);
    }

    public void TryEnqueue(string jobId, IReadOnlyList<StepEvent> events)
    {
        if (!_channel.Writer.TryWrite(new EventBatch(jobId, events)))
        {
            _logger.LogWarning("Event channel full; dropped {Count} events for job {JobId}", events.Count, jobId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await Task.WhenAny(_consumerTask, Task.Delay(DrainTimeout));
    }

    private async Task ConsumeAsync()
    {
        await foreach (var batch in _channel.Reader.ReadAllAsync())
        {
            await SendBatchWithRetryAsync(batch);
        }
    }

    private async Task SendBatchWithRetryAsync(EventBatch batch)
    {
        for (var attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            try
            {
                await SendBatchAsync(batch);
                return;
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                _logger.LogWarning(ex, "XADD batch failed for job {JobId} (attempt {Attempt}); retrying in {Delay}",
                    batch.JobId, attempt + 1, RetryDelays[attempt]);
                await Task.Delay(RetryDelays[attempt]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Dropping {Count} events for job {JobId} after retries exhausted",
                    batch.Events.Count, batch.JobId);
            }
        }
    }

    private async Task SendBatchAsync(EventBatch batch)
    {
        var streamKey = RedisKeys.EventsKey(batch.JobId);
        var pipeline = _database.CreateBatch();
        var pending = new List<Task>(batch.Events.Count);
        foreach (var ev in batch.Events)
        {
            var json = JsonSerializer.Serialize(ev, WireFormat.Json);
            pending.Add(pipeline.StreamAddAsync(streamKey, "data", json));
        }
        pipeline.Execute();
        await Task.WhenAll(pending);
    }

    private readonly record struct EventBatch(string JobId, IReadOnlyList<StepEvent> Events);
}
