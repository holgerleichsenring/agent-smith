using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-9033: a question the spec dialog is waiting on — the payload of the hub's
/// "SpecDialogQuestion" push. The approval that files tickets arrives this way and no
/// other, so a channel that rendered nothing here would leave the gate to time out. The
/// answer travels back as an ordinary message in the dialog, which is why the question id
/// is carried for display rather than for correlation.
/// </summary>
/// <param name="Kind">2026-09-15-cb3e: the question's TYPE, lower-cased for the wire.
/// Slack, Teams and the page all build the approve/reject pair from the TYPE, not from a
/// list, so a surface that rendered only <paramref name="Choices"/> would render nothing on
/// the one question that files real tickets. 2026-09-22-355b: an approval's choices are the
/// other SHAPES the proposal could take, offered beside that pair and never instead of
/// it.</param>
/// <param name="ExpiresAt">2026-09-15-cb3e: when the wait ends, or null where nothing ends
/// it but the next message. Nothing is pushed when a wait expires, so a surface without
/// this keeps offering a button whose click is no longer an answer — it becomes an
/// ordinary message and buys a whole design turn on the word "approve".</param>
public sealed record SpecDialogChannelQuestion(
    string DialogId, string QuestionId, string Kind, string Text,
    IReadOnlyList<DialogChoice> Choices, DateTimeOffset At, DateTimeOffset? ExpiresAt)
{
    /// <summary>The question as this channel carries it.</summary>
    public static SpecDialogChannelQuestion From(
        string dialogId, DialogQuestion question, DateTimeOffset at,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(question);
        return new(dialogId, question.QuestionId, question.Type.ToString().ToLowerInvariant(),
            question.Text, question.Choices ?? [], at,
            expiresAt ?? (question.Timeout > TimeSpan.Zero ? at + question.Timeout : null));
    }
}
