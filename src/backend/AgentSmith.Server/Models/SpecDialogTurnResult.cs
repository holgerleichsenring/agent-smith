using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
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
/// out of the prose; Slack and Teams have no pane, and their confirmation names a phase by id
/// and goal only, so there the reply is the one place its steps and done-list are read.
/// </para>
/// </summary>
public sealed record SpecDialogTurnResult(string Reply, OutcomeProposal Outcome, string Shown)
{
    public static SpecDialogTurnResult On(string platform, string reply, OutcomeProposal outcome) =>
        new(reply, outcome,
            string.Equals(platform, DispatcherDefaults.PlatformDashboard, StringComparison.OrdinalIgnoreCase)
                ? SpecDialogDraftBlocks.Strip(reply)
                : reply);
}
