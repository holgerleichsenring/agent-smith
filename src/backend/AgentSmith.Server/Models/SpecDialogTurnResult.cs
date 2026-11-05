using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Models;

/// <summary>
/// p0315e: what one design-conversation turn produced — the reply, and the typed terminal
/// outcome the outcome flow proposes for confirmation (AnswerOutcome ends the turn with no
/// artifact).
/// <para>
/// The reply comes in two renderings because it has two readers. <paramref name="Reply"/> is
/// what the transcript keeps: the design master's only memory of the conversation, which an
/// edit note re-runs the turn over, so its draft must stay in it. <paramref name="Shown"/> is
/// what the person reads. On the dashboard the draft is shown by the proposal pane and is left
/// out of the prose; Slack and Teams have no pane, and their confirmation names a spec by id
/// and goal only, so there the reply is the one place its steps and done-list are read.
/// </para>
/// <para>
/// 2026-09-17-042ec: <paramref name="Kind"/> is what the router records on the kept turn. It
/// follows the outcome unless the turn says otherwise: a failure note and a notice carry an
/// answer outcome, and neither is a discussion the operator could have replied to.
/// </para>
/// </summary>
/// <para>
/// <paramref name="Stewarded"/>: the ticket steward led the turn. Its reply proposes nothing, so a ```yaml block in
/// it is quoted text the person reads — never stripped as a draft the pane would show.
/// </para>
/// <para>
/// 2026-11-04-96a0a: <paramref name="Subject"/> is the name the master gave the conversation through name_conversation,
/// or null; the keeper stores it when the conversation still has none and the turn did not fail.
/// </para>
/// <para>
/// <paramref name="Thread"/> is what the master last forwarded and was answered, which the keeper stores for the next
/// turn to replay; null on a failed turn.
/// </para>
public sealed record SpecDialogTurnResult(
    string Reply, OutcomeProposal Outcome, string Shown, SpecDialogTurnKind Kind, bool Stewarded = false,
    string? Subject = null, ForwardedThreadCapture? Thread = null)
{
    public static SpecDialogTurnResult On(
        string platform, string reply, OutcomeProposal outcome, SpecDialogTurnKind? kind = null, bool stewarded = false,
        string? subject = null, ForwardedThreadCapture? thread = null) =>
        new(reply, outcome,
            !stewarded && string.Equals(platform, DispatcherDefaults.PlatformDashboard, StringComparison.OrdinalIgnoreCase)
                ? SpecDialogDraftBlocks.Strip(reply)
                : reply,
            kind ?? KindOf(outcome), stewarded, subject, thread);

    public static SpecDialogTurnKind KindOf(OutcomeProposal outcome) => outcome switch
    {
        BugOutcome => SpecDialogTurnKind.BugProposal,
        SpecOutcome => SpecDialogTurnKind.SpecProposal,
        EpicOutcome => SpecDialogTurnKind.EpicProposal,
        TicketGraphOutcome => SpecDialogTurnKind.TicketGraphProposal,
        TicketCommentOutcome => SpecDialogTurnKind.CommentProposal, // 2026-10-28-7a69c
        AnswerOutcome => SpecDialogTurnKind.Answer,
        _ => throw new ArgumentOutOfRangeException(
            nameof(outcome), outcome.GetType().Name, "An outcome with no turn kind would count as a discussion."),
    };
}
