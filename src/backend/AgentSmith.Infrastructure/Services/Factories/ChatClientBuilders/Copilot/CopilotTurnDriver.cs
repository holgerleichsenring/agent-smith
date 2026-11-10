namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: one model call, from provoking it to pricing what it consumed. It owns the
/// pending calls, because remembering what the session is now waiting on is part of finishing a turn.
/// <para>
/// 2026-11-10-e706: every turn waits at most <paramref name="quietBound"/> — the agent's network
/// timeout — for the session's next event, whether a prompt or our answer provoked it. 2026-09-25-6b2e
/// bounded only the resumed turn; a prompted one waited on idle with nothing but the run's token, and
/// a session that never went idle held the run until the wall-time watchdog cut it. The bound is on
/// silence, not on the turn: every event restarts it, so a long answer that keeps arriving is not cut.
/// The client's gate is held across a turn, so one wedged turn would otherwise wedge every later call.
/// </para>
/// </summary>
internal sealed class CopilotTurnDriver(CopilotPendingCalls pending, TimeSpan quietBound)
{
    /// <summary>
    /// Subscribes, provokes the call, and waits for the turn to end — at idle, or as soon as every
    /// tool the assistant asked for has been handed to us, because a session waiting on a pending
    /// tool call never goes idle.
    /// </summary>
    internal async Task<CopilotTurnResult> RunAsync(
        ICopilotSessionHandle session,
        Func<CancellationToken, Task> provoke,
        CancellationToken cancellationToken,
        bool answersPendingCalls = false)
    {
        var before = await session.ReadUsageAsync(cancellationToken);
        var collector = new CopilotTurnCollector(answersPendingCalls);
        using var quiet = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        quiet.CancelAfter(quietBound);
        using (session.Subscribe(evt => Observe(evt, collector, quiet)))
            await WaitForTurnAsync(provoke, collector, quiet.Token, cancellationToken);

        var after = await session.ReadUsageAsync(cancellationToken);
        pending.Remember(collector.ToolRequests);
        return new CopilotTurnResult(
            session.SessionId, collector.Text, collector.Deltas, collector.ToolRequests, after.Since(before));
    }

    private void Observe(CopilotSessionEvent evt, CopilotTurnCollector collector, CancellationTokenSource quiet)
    {
        if (!quiet.IsCancellationRequested) quiet.CancelAfter(quietBound);
        collector.Observe(evt);
    }

    private async Task WaitForTurnAsync(
        Func<CancellationToken, Task> provoke, CopilotTurnCollector collector,
        CancellationToken quiet, CancellationToken cancellationToken)
    {
        try
        {
            await provoke(quiet);
            await collector.WaitAsync(quiet);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The Copilot session sent nothing for {quietBound.TotalSeconds:F0}s (the agent's network "
                + "timeout) — no reply, no tool request and no idle.");
        }
    }
}
