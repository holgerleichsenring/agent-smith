using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-09-07-d5f2 / 2026-09-23-4722a: a Copilot runtime whose sessions replay scripted event
/// sequences — one per provocation, so a tool round trip is scripted as two turns: the model asks
/// for a tool, then answers once the result comes back.
///
/// This is the stand-in the adapter's seam exists for. Without it, pinning the adapter would need
/// a CLI runtime binary and a live Copilot seat.
/// </summary>
internal sealed class FakeCopilotRuntime : ICopilotRuntime
{
    private readonly Queue<CopilotSessionEvent[]> _turns = new();

    internal List<FakeCopilotSession> Sessions { get; } = [];
    internal List<string> DeletedSessions { get; } = [];
    internal List<CopilotSessionRequest> Requests { get; } = [];
    internal CopilotUsage UsageBefore { get; set; } = CopilotUsage.Zero;
    internal CopilotUsage UsageAfter { get; set; } = CopilotUsage.Zero;

    /// <summary>Scripts one turn: the events the next send or tool response replays.</summary>
    internal void Script(params CopilotSessionEvent[] events) => _turns.Enqueue(events);

    public Task<ICopilotSessionHandle> CreateSessionAsync(
        CopilotSessionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var session = new FakeCopilotSession($"session-{Sessions.Count + 1}", this, _turns);
        Sessions.Add(session);
        return Task.FromResult<ICopilotSessionHandle>(session);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        DeletedSessions.Add(sessionId);
        return Task.CompletedTask;
    }

    internal sealed class FakeCopilotSession(
        string sessionId, FakeCopilotRuntime runtime, Queue<CopilotSessionEvent[]> turns) : ICopilotSessionHandle
    {
        private readonly List<Action<CopilotSessionEvent>> _handlers = [];
        private int _usageReads;

        public string SessionId => sessionId;
        internal List<string> Prompts { get; } = [];
        internal List<(string RequestId, string? Result, string? Error)> ToolResponses { get; } = [];

        public IDisposable Subscribe(Action<CopilotSessionEvent> handler)
        {
            _handlers.Add(handler);
            return new Unsubscriber(() => _handlers.Remove(handler));
        }

        public Task SendAsync(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            return ReplayNextTurn(cancellationToken);
        }

        /// <summary>Whether the runtime accepts the next answer — false reproduces a refusal.</summary>
        internal bool AcceptsAnswers { get; set; } = true;

        /// <summary>
        /// 2026-09-25-6b2e: the real runtime DISMISSES the call it was just answered, and the echo
        /// arrives before the resumed turn. Omitting it here is why a turn-killing read of that
        /// notification shipped green: the fake replayed the next turn and nothing ever dismissed.
        /// It is emitted from the answer itself rather than scripted, so a second answer against
        /// one scripted continuation turn still fires its own echo.
        /// </summary>
        public Task<bool> RespondToToolAsync(
            string requestId, string? result, string? error, CancellationToken cancellationToken)
        {
            ToolResponses.Add((requestId, result, error));
            if (!AcceptsAnswers) return Task.FromResult(false);
            Emit(new CopilotSessionEvent.ExternalToolCompleted(requestId));
            return ReplayNextTurn(cancellationToken).ContinueWith(
                _ => true, cancellationToken, TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void Emit(CopilotSessionEvent evt)
        {
            foreach (var handler in _handlers.ToList()) handler(evt);
        }

        /// <summary>
        /// Replays on a worker so the adapter is genuinely awaiting the turn rather than being
        /// handed a finished one on its own stack.
        /// </summary>
        private Task ReplayNextTurn(CancellationToken cancellationToken)
        {
            var events = turns.Count > 0 ? turns.Dequeue() : [];
            var handlers = _handlers.ToList();
            return Task.Run(() =>
            {
                foreach (var evt in events)
                    foreach (var handler in handlers)
                        handler(evt);
            }, cancellationToken);
        }

        public Task<CopilotUsage> ReadUsageAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_usageReads++ == 0 ? runtime.UsageBefore : runtime.UsageAfter);

        public Task AbortAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class Unsubscriber(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }
}
