using System.Text;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Builds the chat texts of the outcome flow — the confirmation question
/// showing the proposed outcome (+ the epic slice shape), and the filed /
/// rejected / timed-out / edit / failure notices. Pure text composition.
/// <para>
/// 2026-09-15-cb3e: composed for the channel that reads it, like every other framework
/// line. This one matters most — it is the text a person approves ticket filing on.
/// </para>
/// </summary>
public sealed class SpecDialogOutcomeComposer
{
    public ComposedReply ComposeConfirmation(OutcomeProposal proposal) => new(m =>
        $"{Describe(proposal, m)}\nApprove to file this outcome, Reject to drop it — "
        + "any other reply is an edit note I will revise the proposal with.");

    public ComposedReply ComposeRejected() => new(_ =>
        "Rejected — nothing was filed. Keep discussing; I will re-propose when the shape changes.");

    public ComposedReply ComposeTimeout() => new(_ =>
        "Confirmation timed out — nothing will be filed. Ask again in this thread when you want me to re-propose.");

    public ComposedReply ComposeEditAck(string note) => new(m =>
        $"Revising the proposal with your note: {m.Italic(note)}");

    public ComposedReply ComposeFiled(OutcomeProposal proposal, FilingReport report) =>
        new(_ => $"Filed {Summarize(proposal)}:\n{FormatTickets(report.Filed)}{FormatNotes(report.Notes)}");

    public ComposedReply ComposeFilingFailure(FilingReport report) => new(_ =>
    {
        var head = report.Filed.Count == 0
            ? "Ticket filing failed — nothing was created."
            : $"Ticket filing failed part-way. Created before the failure:\n{FormatTickets(report.Filed)}";
        return $"{head}{FormatNotes(report.Notes)}\nError: {report.Error}\n"
            + "The confirmed outcome stays stored on this session — ask again in this thread to re-propose and retry.";
    });

    private static string FormatTickets(IReadOnlyList<FiledTicket> filed) =>
        string.Join("\n", filed.Select(t => $"- {t.Reference} — {t.Title}"));

    private static string FormatNotes(IReadOnlyList<string> notes) =>
        notes.Count == 0 ? string.Empty : $"\nNotes:\n{string.Join("\n", notes.Select(n => $"- {n}"))}";

    private static string Describe(OutcomeProposal proposal, SpecDialogMarkup m) => proposal switch
    {
        BugOutcome bug =>
            $"Proposed outcome: {m.Bold("fix-bug ticket")} — {bug.Ticket.Title}\n{bug.Ticket.Description}",
        PhaseOutcome phase =>
            $"Proposed outcome: {m.Bold("one phase")} — `{phase.Draft.PhaseId}` {phase.Draft.Goal}",
        EpicOutcome epic => DescribeEpic(epic, m),
        _ => throw new InvalidOperationException(
            $"Outcome kind '{proposal.GetType().Name}' has no confirmation shape."),
    };

    private static string DescribeEpic(EpicOutcome epic, SpecDialogMarkup m)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Proposed outcome: {m.Bold("epic")} `{epic.Parent.PhaseId}` — {epic.Parent.Goal}");
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
