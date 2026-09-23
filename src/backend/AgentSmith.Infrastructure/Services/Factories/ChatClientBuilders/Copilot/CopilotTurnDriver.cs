namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: one model call, from provoking it to pricing what it consumed. It owns the
/// pending calls, because remembering what the session is now waiting on is part of finishing a turn.
/// </summary>
internal sealed class CopilotTurnDriver(CopilotPendingCalls pending)
{
    /// <summary>
    /// Subscribes, provokes the call, and waits for the turn to end — at idle, or as soon as every
    /// tool the assistant asked for has been handed to us, because a session waiting on a pending
    /// tool call never goes idle.
    /// </summary>
    internal async Task<CopilotTurnResult> RunAsync(
        ICopilotSessionHandle session,
        Func<CancellationToken, Task> provoke,
        CancellationToken cancellationToken)
    {
        var before = await session.ReadUsageAsync(cancellationToken);
        var collector = new CopilotTurnCollector();
        using (session.Subscribe(collector.Observe))
        {
            await provoke(cancellationToken);
            await collector.WaitAsync(cancellationToken);
        }

        var after = await session.ReadUsageAsync(cancellationToken);
        pending.Remember(collector.ToolRequests);
        return new CopilotTurnResult(
            session.SessionId, collector.Text, collector.Deltas, collector.ToolRequests, after.Since(before));
    }
}
