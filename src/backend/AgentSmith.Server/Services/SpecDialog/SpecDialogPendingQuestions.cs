using System.Collections.Concurrent;
using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-15-cb3e: the question a turn is blocked on, and when the wait ends — null where
/// nothing ends it but the next message, which is how a design turn's own ask_human waits.
/// </summary>
public sealed record PendingQuestion(DialogQuestion Question, DateTimeOffset? ExpiresAt);

/// <summary>
/// p0315b: the ask_human question a running design turn is currently blocked
/// on, keyed by session id. In-memory by design — the waiting loop lives in
/// this process; if the process dies the wait dies with it, so durable state
/// would only pin ghosts. Singleton.
/// <para>
/// 2026-09-15-cb3e: it holds the QUESTION now, not just its id. A browser reload does not
/// end the server's wait, but it did end the operator's only copy of what they were being
/// asked — the approval that files tickets arrives as a hub push and lived nowhere else, so
/// a refresh during the gate left a blocked master and a page with no question and no
/// button. The deadline travels with it for the same reason: nothing tells the page when a
/// wait has expired, so without it an expired card keeps offering a button whose click is
/// no longer an answer.
/// </para>
/// <para>
/// 2026-09-18-2f8b: SETTING ONE STOPS THE TURN'S CLOCK AND TAKING IT STARTS IT AGAIN. A design
/// turn's ask_human blocks INSIDE the turn's execution, so the turn outlives the wait and a
/// duration measured from its start instant would count it: a person who took twenty minutes
/// to answer would then be shown a turn that had "worked" for twenty minutes. The boundary is
/// HERE rather than at the three sites that ask and answer, because this class is what "a turn
/// is blocked" means and a fourth site would otherwise have to remember the rule.
/// </para>
/// </summary>
public sealed class SpecDialogPendingQuestions(SpecDialogTurnGate turns)
{
    private readonly ConcurrentDictionary<string, PendingQuestion> _pending =
        new(StringComparer.Ordinal);

    public void Set(string sessionId, DialogQuestion question, DateTimeOffset? expiresAt)
    {
        _pending[sessionId] = new PendingQuestion(question, expiresAt);
        turns.Blocked(sessionId);
    }

    /// <summary>
    /// A design turn's own ask_human, which arrives off the bus as an id and a line of text.
    /// It is free text with no deadline: the wait ends when the person says something.
    /// </summary>
    public void Set(string sessionId, string questionId, string text) =>
        Set(sessionId,
            new DialogQuestion(
                questionId, QuestionType.FreeText, text, Context: null, Choices: null,
                DefaultAnswer: null, Timeout: TimeSpan.Zero),
            expiresAt: null);

    /// <summary>The question without consuming it — what a page rebuilding itself needs.</summary>
    public bool TryPeek(string sessionId, out PendingQuestion pending) =>
        _pending.TryGetValue(sessionId, out pending!);

    /// <summary>
    /// Consumes the question only while it is still the one that was peeked, so an answer is
    /// recorded by what it answered and never handed to a question that replaced it (2026-09-17-042el).
    /// </summary>
    public bool TryTake(string sessionId, PendingQuestion peeked)
    {
        if (!_pending.TryRemove(KeyValuePair.Create(sessionId, peeked))) return false;
        turns.Resumed(sessionId);
        return true;
    }

    /// <summary>The wait is over without an answer — a timeout, or the turn ending.</summary>
    public void Clear(string sessionId)
    {
        _pending.TryRemove(sessionId, out _);
        turns.Resumed(sessionId);
    }
}
