using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// 2026-11-18-c8e2a: what waits for one reader of a sub-agent team — a child's word from its lead, or the lead's
/// notices of children that finished. It is a mid-turn message source like the operator's, so it reaches its reader
/// the same way: appended to the reader's next tool result.
/// </summary>
public sealed class SubAgentInbox : IMidTurnMessages
{
    private readonly Lock _gate = new();
    private readonly List<MidTurnMessage> _waiting = [];

    /// <summary>How many messages wait to be read.</summary>
    public int Count
    {
        get { lock (_gate) return _waiting.Count; }
    }

    public void Post(string text, string heading)
    {
        lock (_gate) _waiting.Add(new MidTurnMessage(text, DateTimeOffset.UtcNow, heading));
    }

    /// <summary>A team inbox leads nothing; the operator's source is told who leads.</summary>
    public void Lead(bool stewarded) { }

    public ValueTask<IReadOnlyList<MidTurnMessage>> DrainAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Drain());

    /// <summary>Takes every waiting message, oldest first; each is handed out once.</summary>
    public IReadOnlyList<MidTurnMessage> Drain()
    {
        lock (_gate)
        {
            if (_waiting.Count == 0) return [];
            var taken = _waiting.ToList();
            _waiting.Clear();
            return taken;
        }
    }
}
