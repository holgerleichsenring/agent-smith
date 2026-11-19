namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-10-29-7f2d: whether the first probe pass has run. The pass moved behind the listener, so the
/// readers of its answer — the findings endpoint and, since 2026-08-24-035a, the readiness endpoint —
/// wait for it rather than report an unchecked server as a clean one. A singleton of its own rather than
/// a member of the runner, because a runner that could not be built must still let the endpoints answer.
/// </summary>
public sealed class StartupProbePass(TimeProvider? clock = null)
{
    /// <summary>The longest a findings read waits: the pass's probes are each bounded, the read is too.</summary>
    public static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(15);

    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly DateTimeOffset _created = (clock ?? TimeProvider.System).GetUtcNow();

    public void Complete() => _done.TrySetResult();

    /// <summary>
    /// 2026-08-24-035a: the pass completed, or <see cref="ReadBudget"/> has gone by since this pass was
    /// created (just before the host starts), so a pass with a hanging probe never holds readiness forever.
    /// </summary>
    public bool IsSettled => _done.Task.IsCompleted || _clock.GetUtcNow() - _created >= ReadBudget;

    /// <summary>Returns once the pass completed, or after <paramref name="budget"/>, whichever comes first.</summary>
    public async Task WaitAsync(TimeSpan budget, CancellationToken cancellationToken)
    {
        try { await _done.Task.WaitAsync(budget, cancellationToken); }
        catch (TimeoutException) { /* the budget is the answer: read what has been recorded so far */ }
    }
}
