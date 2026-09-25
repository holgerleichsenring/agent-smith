namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>
/// 2026-09-23-4722a: one model call, from provoking it to pricing what it consumed. It owns the
/// pending calls, because remembering what the session is now waiting on is part of finishing a turn.
/// </summary>
internal sealed class CopilotTurnDriver(CopilotPendingCalls pending)
{
    /// <summary>2026-09-25-6b2e: the deadline a resumed turn is given. A turn provoked by a PROMPT
    /// ends when the model answers or asks for its tools, both bounded by the model itself. A turn
    /// provoked by our ANSWER has neither: if the runtime dismisses the call and never resumes,
    /// nothing else would end the wait, and the client's gate is held across it — one wedged turn
    /// would wedge every later call. 300s is the network timeout this repository settled on in
    /// p0235.</summary>
    private static readonly TimeSpan ResumeDeadline = TimeSpan.FromSeconds(300);

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
        using (session.Subscribe(collector.Observe))
        {
            await provoke(cancellationToken);
            await WaitForTurnAsync(collector, answersPendingCalls, cancellationToken);
        }

        var after = await session.ReadUsageAsync(cancellationToken);
        pending.Remember(collector.ToolRequests);
        return new CopilotTurnResult(
            session.SessionId, collector.Text, collector.Deltas, collector.ToolRequests, after.Since(before));
    }

    private static async Task WaitForTurnAsync(
        CopilotTurnCollector collector, bool answersPendingCalls, CancellationToken cancellationToken)
    {
        if (!answersPendingCalls)
        {
            await collector.WaitAsync(cancellationToken);
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(ResumeDeadline);
        try
        {
            await collector.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                $"The Copilot session took the tool answer but did not resume within "
                + $"{ResumeDeadline.TotalSeconds:F0}s — no reply, no further tool request and no idle.");
        }
    }
}
