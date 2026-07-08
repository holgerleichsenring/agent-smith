using System.Collections.Concurrent;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: one design turn per session at a time. In-memory by design — a
/// running turn is an in-process agentic loop, so the guard's lifetime is
/// exactly the process's. Singleton.
/// </summary>
public sealed class SpecDialogTurnGate
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.Ordinal);

    public bool TryEnter(string sessionId) => _running.TryAdd(sessionId, 0);

    public void Exit(string sessionId) => _running.TryRemove(sessionId, out _);
}
