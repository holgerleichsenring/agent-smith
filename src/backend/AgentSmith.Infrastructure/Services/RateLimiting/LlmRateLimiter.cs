using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0188: admission to one deployment. A call proceeds when its requests and tokens fit (<see cref="DeploymentQuota"/>);
/// callers queue oldest first, and the head of the queue waits for the refill.
/// <para>
/// 2026-11-22-66e4a: it counts what the provider counts — a call reserves its learned-ratio estimate plus, where the
/// provider's admission counts it, its max_tokens (<see cref="QuotaReservation"/>), and its lease settles to the
/// provider's own count. A call that will wait a second or more says so first, with the wait the queue ahead of it
/// needs; the head re-checks after every wait, so a refusal that pauses the deployment holds it too.
/// </para>
/// </summary>
internal sealed class LlmRateLimiter : ILlmRateLimiter
{
    /// <summary>A wait shorter than this is not announced.</summary>
    internal static readonly TimeSpan AnnounceFrom = TimeSpan.FromSeconds(1); // not a limit: below a second a wait is not worth a line on the run

    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly object _queueLock = new();
    private readonly TimeProvider _clock;
    private readonly DeploymentQuota _quota;
    private TaskCompletionSource _resized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private double _queuedTokens;
    private int _queuedCalls;

    public LlmRateLimiter(LlmRateLimitOptions options, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _quota = new DeploymentQuota(options, _clock);
    }

    public async Task<ILlmQuotaLease> AcquireAsync(LlmQuotaDemand demand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(demand);
        var reserved = _quota.Reserve(demand);
        if (reserved.Needed > _quota.TokenCapacity && demand.OnShortfall is { } shortfall) // 2026-11-22-ab45
            await shortfall(new(reserved.Needed, reserved.Input, reserved.Output, _quota.TokenCapacity), cancellationToken);
        var expected = Enqueue(reserved.Total);
        try
        {
            if (expected >= AnnounceFrom && demand.OnWait is { } onWait)
                await onWait(expected, _quota.Paused() > TimeSpan.Zero, cancellationToken);
            await WaitYourTurnAsync(reserved.Total, cancellationToken);
        }
        finally
        {
            Dequeue(reserved.Total);
        }
        return new LlmQuotaLease(_quota, demand.EstimatedInputTokens, reserved, _clock);
    }

    /// <summary>2026-11-22-66e4b: the configured budget changed.</summary>
    internal void Adopt(LlmRateLimitOptions options)
    {
        _quota.Resize(options);
        Interlocked.Exchange(ref _resized, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }

    internal LlmQuotaSnapshot Snapshot(ModelQuotaScope scope)
    {
        int waiting;
        lock (_queueLock) waiting = _queuedCalls;
        return _quota.Snapshot(scope, waiting);
    }

    // Joins the queue and says how long the queue ahead, this call included, needs to fit.
    private TimeSpan Enqueue(int tokens)
    {
        double queuedTokens;
        int queuedCalls;
        lock (_queueLock)
        {
            queuedTokens = _queuedTokens += tokens;
            queuedCalls = ++_queuedCalls;
        }
        return _quota.TakeOrWait(queuedCalls, queuedTokens, take: false);
    }

    private void Dequeue(int tokens)
    {
        lock (_queueLock) { _queuedTokens -= tokens; _queuedCalls--; }
    }

    // Oldest first: one caller at a time waits at the head for the refill; the head re-checks after every wait, and a
    // changed budget (66e4b) wakes it at once.
    private async Task WaitYourTurnAsync(int tokens, CancellationToken cancellationToken)
    {
        await _turn.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                // Taken BEFORE asking, so a budget changed in between still wakes this wait.
                var resized = Volatile.Read(ref _resized).Task;
                var wait = _quota.TakeOrWait(1, tokens, take: true);
                if (wait <= TimeSpan.Zero) return;
                await Task.WhenAny(Task.Delay(wait, _clock, cancellationToken), resized);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        finally
        {
            _turn.Release();
        }
    }
}
