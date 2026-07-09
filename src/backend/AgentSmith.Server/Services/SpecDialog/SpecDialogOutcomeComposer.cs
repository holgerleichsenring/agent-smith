using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Builds the chat texts of the outcome flow — the confirmation question
/// showing the proposed outcome (+ the epic slice shape), and the filed /
/// rejected / timed-out / edit / failure notices. Pure text composition.
/// </summary>
public sealed class SpecDialogOutcomeComposer
{
    public string ComposeConfirmation(OutcomeProposal proposal) =>
        $"{Describe(proposal)}\nApprove to file this outcome, Reject to drop it — "
        + "any other reply is an edit note I will revise the proposal with.";

    public string ComposeRejected() =>
        "Rejected — nothing was filed. Keep discussing; I will re-propose when the shape changes.";

    public string ComposeTimeout() =>
        "Confirmation timed out — nothing will be filed. Ask again in this thread when you want me to re-propose.";

    public string ComposeEditAck(string note) =>
        $"Revising the proposal with your note: _{note}_";

    public string ComposeFiled(OutcomeProposal proposal, IReadOnlyList<FiledTicket> filed) =>
        $"Filed {Summarize(proposal)}:\n{FormatTickets(filed)}";

    public string ComposeFilingFailure(string error, IReadOnlyList<FiledTicket> filed)
    {
        var head = filed.Count == 0
            ? "Ticket filing failed — nothing was created."
            : $"Ticket filing failed part-way. Created before the failure:\n{FormatTickets(filed)}";
        return $"{head}\nError: {error}\n"
            + "The confirmed outcome stays stored on this session — ask again in this thread to re-propose and retry.";
    }

    private static string FormatTickets(IReadOnlyList<FiledTicket> filed) =>
        string.Join("\n", filed.Select(t => $"- {t.Reference} — {t.Title}"));

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
            $"Outcome kind '{proposal.GetType().Name}' has no summary shape."),
    };
}
