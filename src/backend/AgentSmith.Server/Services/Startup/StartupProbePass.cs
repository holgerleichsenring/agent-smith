namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-10-29-7f2d: whether the first probe pass has run. The pass moved behind the listener, so the one
/// reader of its answer — the findings endpoint — waits for it rather than report an empty list as a
/// clean bill. A singleton of its own rather than a member of the runner, because a runner that could
/// not be built must still let the endpoint answer.
/// </summary>
public sealed class StartupProbePass
{
    /// <summary>The longest a findings read waits: the pass's probes are each bounded, the read is too.</summary>
    public static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(15);

    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Complete() => _done.TrySetResult();

    /// <summary>Returns once the pass completed, or after <paramref name="budget"/>, whichever comes first.</summary>
    public async Task WaitAsync(TimeSpan budget, CancellationToken cancellationToken)
    {
        try { await _done.Task.WaitAsync(budget, cancellationToken); }
        catch (TimeoutException) { /* the budget is the answer: read what has been recorded so far */ }
    }
}
