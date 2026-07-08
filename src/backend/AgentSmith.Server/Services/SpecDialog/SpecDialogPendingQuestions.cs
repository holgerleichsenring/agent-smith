using System.Collections.Concurrent;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: the ask_human question a running design turn is currently blocked
/// on, keyed by session id. In-memory by design — the waiting loop lives in
/// this process; if the process dies the wait dies with it, so durable state
/// would only pin ghosts. Singleton.
/// </summary>
public sealed class SpecDialogPendingQuestions
{
    private readonly ConcurrentDictionary<string, string> _pending = new(StringComparer.Ordinal);

    public void Set(string sessionId, string questionId) => _pending[sessionId] = questionId;

    public bool TryTake(string sessionId, out string questionId) =>
        _pending.TryRemove(sessionId, out questionId!);

    public void Clear(string sessionId) => _pending.TryRemove(sessionId, out _);
}
