using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Server-side pipeline-scoped channel onto the Redis bus shared with the Agent.
/// One instance per pipeline run; persistent _lastSeenXid prevents replaying earlier
/// events through IProgress on subsequent steps.
/// </summary>
public sealed class SandboxRedisChannel : IAsyncDisposable
{
    private static readonly TimeSpan ResultPollTick = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan EventBlockTick = TimeSpan.FromMilliseconds(1000);

    private readonly IDatabase _database;
    private readonly string _jobId;
    private readonly ILogger _logger;
    private string _lastSeenXid = "0-0";

    public SandboxRedisChannel(IConnectionMultiplexer multiplexer, string jobId, ILogger logger)
    {
        _database = multiplexer.GetDatabase();
        _jobId = jobId;
        _logger = logger;
    }

    public async Task PushStepAsync(Step step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(step, WireFormat.Json);
        await _database.ListLeftPushAsync(RedisKeys.InputKey(_jobId), json);
    }

    public async Task<StepResult> WaitForResultAsync(
        Guid stepId,
        IProgress<StepEvent>? progress,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DrainEventsAsync(stepId, progress);
            var result = await TryPopResultAsync(stepId, cancellationToken);
            if (result is not null) return result;
            await Task.Delay(ResultPollTick, cancellationToken);
        }
        throw new TimeoutException(
            $"Sandbox step {stepId} did not produce a result within {timeout.TotalSeconds:F0}s");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _database.KeyDeleteAsync([
                RedisKeys.InputKey(_jobId),
                RedisKeys.EventsKey(_jobId),
                RedisKeys.ResultsKey(_jobId)
            ]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up Redis keys for job {JobId}", _jobId);
        }
    }

    private async Task DrainEventsAsync(Guid stepId, IProgress<StepEvent>? progress)
    {
        var streamKey = RedisKeys.EventsKey(_jobId);
        var entries = await _database.StreamReadAsync(streamKey, _lastSeenXid, count: 100);
        if (entries.Length == 0) return;

        foreach (var entry in entries)
        {
            _lastSeenXid = entry.Id!;
            if (progress is null) continue;
            ForwardMatchingEvent(entry, stepId, progress);
        }
    }

    private void ForwardMatchingEvent(StreamEntry entry, Guid stepId, IProgress<StepEvent> progress)
    {
        try
        {
            var raw = entry["data"];
            if (raw.IsNullOrEmpty) return;
            var ev = JsonSerializer.Deserialize<StepEvent>(raw!, WireFormat.Json);
            if (ev is not null && ev.StepId == stepId) progress.Report(ev);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize StepEvent {Id}", entry.Id);
        }
    }

    private async Task<StepResult?> TryPopResultAsync(Guid stepId, CancellationToken cancellationToken)
    {
        var key = RedisKeys.ResultsKey(_jobId);
        var value = await _database.ListRightPopAsync(key);
        if (value.IsNull) return null;

        var result = JsonSerializer.Deserialize<StepResult>(value!, WireFormat.Json);
        if (result is null) return null;
        if (result.StepId == stepId) return result;

        _logger.LogWarning("Discarded stale StepResult for step {StepId} (waiting for {WaitingFor})",
            result.StepId, stepId);
        return null;
    }
}
