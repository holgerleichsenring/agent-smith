using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-09-07-d5f2: a Copilot runtime whose sessions replay a scripted event sequence.
/// This is the stand-in the adapter's seam exists for — without it, pinning the adapter would need
/// a CLI runtime binary and a live Copilot seat.
/// </summary>
internal sealed class FakeCopilotRuntime : ICopilotRuntime
{
    private readonly List<CopilotSessionEvent> _script = [];

    internal List<FakeCopilotSession> Sessions { get; } = [];
    internal List<string> DeletedSessions { get; } = [];
    internal List<CopilotSessionRequest> Requests { get; } = [];
    internal CopilotUsage UsageBefore { get; set; } = CopilotUsage.Zero;
    internal CopilotUsage UsageAfter { get; set; } = CopilotUsage.Zero;

    /// <summary>Sets the events the NEXT send replays, in order.</summary>
    internal void Script(params CopilotSessionEvent[] events)
    {
        _script.Clear();
        _script.AddRange(events);
    }

    public Task<ICopilotSessionHandle> CreateSessionAsync(
        CopilotSessionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var session = new FakeCopilotSession($"session-{Sessions.Count + 1}", this, _script);
        Sessions.Add(session);
        return Task.FromResult<ICopilotSessionHandle>(session);
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        DeletedSessions.Add(sessionId);
        return Task.CompletedTask;
    }

    internal sealed class FakeCopilotSession(
        string sessionId, FakeCopilotRuntime runtime, List<CopilotSessionEvent> script) : ICopilotSessionHandle
    {
        private readonly List<Action<CopilotSessionEvent>> _handlers = [];
        private int _usageReads;

        public string SessionId => sessionId;
        internal List<string> Prompts { get; } = [];

        public IDisposable Subscribe(Action<CopilotSessionEvent> handler)
        {
            _handlers.Add(handler);
            return new Unsubscriber(() => _handlers.Remove(handler));
        }

        public Task SendAsync(string prompt, CancellationToken cancellationToken)
        {
            Prompts.Add(prompt);
            // Replay on a worker so the adapter is genuinely awaiting idle rather than being
            // handed a completed turn on its own stack.
            var handlers = _handlers.ToList();
            var events = script.ToList();
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
