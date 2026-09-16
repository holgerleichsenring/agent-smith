using AgentSmith.Contracts.Dialogue;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-09-15-9033: a question the spec dialog is waiting on — the payload of the hub's
/// "SpecDialogQuestion" push. The approval that files tickets arrives this way and no
/// other, so a channel that rendered nothing here would leave the gate to time out. The
/// answer travels back as an ordinary message in the dialog, which is why the question id
/// is carried for display rather than for correlation.
/// </summary>
public sealed record SpecDialogChannelQuestion(
    string DialogId, string QuestionId, string Text,
    IReadOnlyList<DialogChoice> Choices, DateTimeOffset At);
