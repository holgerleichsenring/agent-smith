using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-12-a1fa: the operator messages that arrived while a design turn held a session's gate, per process like the
/// gate beside it. Adding, draining and the router's exit share one lock, so a message is either queued while the
/// turn still holds the gate — and the router runs it before it lets go — or finds the gate as anything else.
/// Singleton.
/// </summary>
public sealed class SpecDialogOperatorInbox(SpecDialogTurnGate gate)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<QueuedOperatorMessage>> _queued = new(StringComparer.Ordinal);

    /// <summary>Queues the message when a router design turn holds the session's gate; false otherwise.</summary>
    public bool TryQueue(string sessionId, QueuedOperatorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_lock)
        {
            if (!gate.HoldsDesignTurn(sessionId)) return false;
            if (!_queued.TryGetValue(sessionId, out var queue)) _queued[sessionId] = queue = [];
            queue.Add(message);
            return true;
        }
    }

    /// <summary>2026-11-12-92e9d: whether a message waits for the session's running turn.</summary>
    public bool IsWaiting(string sessionId)
    {
        lock (_lock) return _queued.TryGetValue(sessionId, out var queue) && queue.Count > 0;
    }

    /// <summary>The inbox as the turn about to run reads it: its poster's messages, recorded on its capture.</summary>
    public IMidTurnMessages For(string sessionId, TurnPoster poster, ForwardedThreadCapture capture) =>
        new SpecDialogTurnMessages(this, gate, sessionId, poster, capture);

    /// <summary>Takes the queued messages of <paramref name="poster"/>, oldest first; another poster's stay.</summary>
    public IReadOnlyList<QueuedOperatorMessage> Drain(string sessionId, TurnPoster poster)
    {
        ArgumentNullException.ThrowIfNull(poster);
        return Take(sessionId, m => string.Equals(m.Poster.Id, poster.Id, StringComparison.Ordinal));
    }

    /// <summary>Takes every queued message stored before the transcript reached <paramref name="length"/>: the next
    /// turn's prompt holds them.</summary>
    public IReadOnlyList<QueuedOperatorMessage> TakeBefore(string sessionId, int length) =>
        Take(sessionId, m => m.Index < length);

    /// <summary>Releases the gate when nothing is queued; false, holding it, when something is.</summary>
    public bool TryExit(string sessionId)
    {
        lock (_lock)
        {
            if (_queued.TryGetValue(sessionId, out var queue) && queue.Count > 0) return false;
            _queued.Remove(sessionId);
            gate.Exit(sessionId);
            return true;
        }
    }

    /// <summary>Releases the gate after a turn that threw, dropping what waited; those turns stay in the transcript.</summary>
    public void Abandon(string sessionId)
    {
        lock (_lock)
        {
            _queued.Remove(sessionId);
            gate.Exit(sessionId);
        }
    }

    /// <summary>Forgets what waited for a conversation that is gone.</summary>
    public void Drop(string sessionId)
    {
        lock (_lock) _queued.Remove(sessionId);
    }

    private List<QueuedOperatorMessage> Take(string sessionId, Func<QueuedOperatorMessage, bool> taken)
    {
        lock (_lock)
        {
            if (!_queued.TryGetValue(sessionId, out var queue)) return [];
            var took = queue.Where(taken).ToList();
            queue.RemoveAll(m => taken(m));
            return took;
        }
    }
}
