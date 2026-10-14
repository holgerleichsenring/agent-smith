using System.Text.Json;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Server-side pipeline-scoped channel onto the Redis bus shared with the Agent.
/// One instance per pipeline run; the drain's persistent cursor prevents replaying
/// earlier events through IProgress on subsequent steps.
/// </summary>
public sealed class SandboxRedisChannel : IAsyncDisposable
{
    private static readonly TimeSpan ResultPollTick = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan EventBlockTick = TimeSpan.FromMilliseconds(1000);

    private readonly IDatabase _database;
    private readonly string _jobId;
    private readonly ILogger _logger;
    private readonly SandboxEventDrain _drain;
    private readonly SandboxResultList _results;
    // 2026-10-13-3890d: the agent runs one step at a time and every result lands on one list,
    // so a second caller waiting at the same time pops — and discards — the first one's result,
    // and the shared drain cursor skips its output. One step per channel, end to end.
    private readonly SemaphoreSlim _oneStep = new(1, 1);

    public SandboxRedisChannel(
        IConnectionMultiplexer multiplexer, string jobId, ILogger logger,
        IWireProtocolWatcher protocol)
    {
        _database = multiplexer.GetDatabase();
        _jobId = jobId;
        _logger = logger;
        _results = new SandboxResultList(_database, jobId, logger, protocol);
        _drain = new SandboxEventDrain(_database, RedisKeys.EventsKey(jobId), logger);
    }

    /// <summary>
    /// 2026-10-13-3890d: push a step and wait for its own result, one caller at a time. The wait
    /// starts once this caller holds the channel, so a step queued behind a long sibling is not
    /// timed out before the agent ever sees it.
    /// </summary>
    public async Task<StepResult> RunStepAsync(
        Step step, IProgress<StepEvent>? progress, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await _oneStep.WaitAsync(cancellationToken);
        try
        {
            await PushStepAsync(step, cancellationToken);
            return await WaitForResultAsync(step.StepId, progress, timeout, cancellationToken);
        }
        finally
        {
            _oneStep.Release();
        }
    }

    public async Task PushStepAsync(Step step, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(step, WireFormat.Json);
        await _database.ListLeftPushAsync(RedisKeys.InputKey(_jobId), json);
        await ExpireJobKeysAsync();
    }

    // 2026-10-02-5ab2f: dispose deletes the keys, but a server that crashed never disposes;
    // refreshing the expiry on every push bounds what a dead job leaves behind.
    private async Task ExpireJobKeysAsync()
    {
        await _database.KeyExpireAsync(RedisKeys.InputKey(_jobId), RedisKeys.JobKeyTtl);
        await _database.KeyExpireAsync(RedisKeys.ResultsKey(_jobId), RedisKeys.JobKeyTtl);
        await _database.KeyExpireAsync(RedisKeys.EventsKey(_jobId), RedisKeys.JobKeyTtl);
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
            await _drain.DrainAsync(stepId, progress);
            var result = await _results.TryPopAsync(stepId);
            if (result is not null)
            {
                // p0491: the agent writes its last output lines just before pushing the
                // result, so returning here without a second drain leaves them for a later
                // step to discard — which is how a command's whole output disappeared.
                await _drain.DrainAsync(stepId, progress);
                return result;
            }
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
}
