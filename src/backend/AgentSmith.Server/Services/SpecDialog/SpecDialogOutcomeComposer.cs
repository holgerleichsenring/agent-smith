using System.Text;
using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: builds the chat texts of the outcome flow — the confirmation
/// question showing the proposed outcome (+ the epic slice shape), and the
/// confirmed / declined / timed-out notices. Pure text composition.
/// </summary>
public sealed class SpecDialogOutcomeComposer
{
    public string ComposeConfirmation(OutcomeProposal proposal) =>
        $"{Describe(proposal)}\nReply `approve` to confirm this outcome — anything else keeps discussing.";

    public string ComposeDeclined() =>
        "Not confirmed — nothing will be filed. Keep discussing; I will re-propose when the shape changes.";

    public string ComposeTimeout() =>
        "Confirmation timed out — nothing will be filed. Ask again in this thread when you want me to re-propose.";

    public string ComposeStored(ConversationState state, OutcomeProposal proposal) =>
        $"Confirmed and stored on session `{state.JobId}`: {Summarize(proposal)}. "
        + "Nothing has been filed yet — ticket filing ships with /create-phase (p0315c).";

    private static string Describe(OutcomeProposal proposal) => proposal switch
    {
        BugOutcome bug =>
            $"Proposed outcome: *fix-bug ticket* — {bug.Ticket.Title}\n{bug.Ticket.Description}",
        PhaseOutcome phase =>
            $"Proposed outcome: *one phase* — `{phase.Draft.PhaseId}` {phase.Draft.Goal}",
        EpicOutcome epic => DescribeEpic(epic),
        _ => throw new InvalidOperationException(
            $"Outcome kind '{proposal.GetType().Name}' has no confirmation shape."),
    };

    private static string DescribeEpic(EpicOutcome epic)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Proposed outcome: *epic* `{epic.Parent.PhaseId}` — {epic.Parent.Goal}");
        sb.AppendLine("Slices, in order:");
        foreach (var child in epic.Children)
            sb.AppendLine($"- `{child.PhaseId}` {child.Goal}{FormatRequires(child)}");
        return sb.ToString().TrimEnd();
    }

    private static string FormatRequires(PhaseDraft draft) =>
        draft.Requires.Count == 0 ? string.Empty : $" (requires: {string.Join(", ", draft.Requires)})";

    private static string Summarize(OutcomeProposal proposal) => proposal switch
    {
        BugOutcome bug => $"a fix-bug ticket ('{bug.Ticket.Title}') for the fix-bug pipeline",
        PhaseOutcome phase => $"one phase (`{phase.Draft.PhaseId}`)",
        EpicOutcome epic =>
            $"an epic (`{epic.Parent.PhaseId}` + {epic.Children.Count} linked child phases)",
        _ => throw new InvalidOperationException(
            $"Outcome kind '{proposal.GetType().Name}' has no stored-notice shape."),
    };
}
