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
    /// <summary>
    /// 2026-09-17-042ek: on the dialog PAGE this carries nothing. The approval card already
    /// renders a counted summary of what would be filed and 2026-09-17-042ed's findings above
    /// it, and the buttons beside it are the approve/reject sentence — a server line saying
    /// any of that would show it a second time. The card's own fallback covers the one case
    /// where it has no proposal to summarise. Chat has no such card and keeps the text.
    /// </summary>
    public ComposedReply ComposeConfirmation(OutcomeProposal proposal) => new(m =>
        m.Wording(
            $"{Describe(proposal, m)}\n{Findings(proposal, m)}"
            // 2026-09-25-8e51e: "or an offered button" — one of them now AMENDS the ticket this
            // conversation belongs to rather than sending the proposal round again.
            + "Approve to file this outcome, Reject to drop it, or pick one of the offered "
            + "buttons — any other reply is an edit note I will revise the proposal with.",
            string.Empty));

    /// <summary>
    /// 2026-09-17-042ed: what the turn's own review found, above the question it is asked with —
    /// the person approving reads the objection and the line that proves it, not a summary.
    /// </summary>
    private static string Findings(OutcomeProposal proposal, SpecDialogMarkup m)
    {
        if (proposal.Findings.Count == 0) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine($"{m.Bold("The review of this proposal found:")}");
        foreach (var finding in proposal.Findings)
            sb.AppendLine(
                $"- {finding.SpecId} — {finding.Problem}: {finding.Why}"
                + (finding.Evidence is null
                    ? finding.Quote is null ? string.Empty : $" (\"{finding.Quote}\")"
                    : $" (evidence: {finding.Evidence})"));
        return sb.ToString();
    }

    public ComposedReply ComposeRejected() => new(_ =>
        "Rejected — nothing was filed. Keep discussing; I will re-propose when the shape changes.");

    /// <summary>2026-11-10-ccd9: a newer proposal took the place of the one awaiting a decision.</summary>
    public ComposedReply ComposeSuperseded() => new(_ =>
        "The earlier proposal is replaced by the one below; it can no longer be approved.");

    /// <summary>2026-11-10-ccd9: a decision on a card that is no longer the one awaiting.</summary>
    public ComposedReply ComposeReplaced(bool another) => new(m => another
        ? "That proposal was replaced; the current proposal is " + m.Wording("posted below.", "below.")
        : "That proposal is no longer open; nothing was filed.");

    public ComposedReply ComposeFiled(OutcomeProposal proposal, FilingReport report) =>
        new(m => $"Filed {Summarize(proposal)}:\n{FormatTickets(report.Filed, m)}{FormatNotes(report.Notes)}");

    /// <param name="proposal">2026-10-14-dbcel: a list that created a ticket must not be approved whole again.</param>
    public ComposedReply ComposeFilingFailure(FilingReport report, OutcomeProposal? proposal = null) => new(m =>
    {
        var head = report.Filed.Count == 0
            ? "Ticket filing failed — nothing was created."
            : $"Ticket filing failed part-way. Created before the failure:\n{FormatTickets(report.Filed, m)}";
        return $"{head}{FormatNotes(report.Notes)}\nError: {report.Error}\n" + (proposal is TicketGraphOutcome && report.Filed.Count > 0
            ? "Approving again files the whole list again: close the created tickets, or propose the rest naming them with `ticket:<id>`."
            : "The confirmed outcome stays stored on this session — ask again " + m.Wording("in this thread ", "below ") + "to re-propose and retry.");
    });

    private static string FormatTickets(IReadOnlyList<FiledTicket> filed, SpecDialogMarkup m) =>
        string.Join("\n", filed.Select(t => $"- {FiledTicketKey.Named(t, m)} — {t.Title}{t.Start?.Note}"));

    private static string FormatNotes(IReadOnlyList<string> notes) =>
        notes.Count == 0 ? string.Empty : $"\nNotes:\n{string.Join("\n", notes.Select(n => $"- {n}"))}";

    private static string Describe(OutcomeProposal proposal, SpecDialogMarkup m) => proposal switch
    {
        BugOutcome bug =>
            $"Proposed outcome: {m.Bold("bug ticket")} — {bug.Ticket.Title}\n{bug.Ticket.Description}",
        SpecOutcome spec =>
            $"Proposed outcome: {m.Bold("one spec")} — `{spec.Draft.SpecId}` {spec.Draft.Goal}",
        EpicOutcome epic => EpicProposalText.Describe(epic, m),
        TicketGraphOutcome graph => TicketGraphProposalText.Describe(graph, m),
        TicketCommentOutcome comment => $"Proposed outcome: {m.Bold("a comment on the ticket")}, and nothing starts\n{comment.Comment}",
        _ => throw new InvalidOperationException(
            $"Outcome kind '{proposal.GetType().Name}' has no confirmation shape."),
    };

    private static string Summarize(OutcomeProposal proposal) => proposal switch
    {
        BugOutcome bug => $"a bug ticket ('{bug.Ticket.Title}') for the code pipeline",
        SpecOutcome spec => $"one spec (`{spec.Draft.SpecId}`)",
        // 2026-09-17-0e79d: an epic is ONE work ticket, and 2026-09-22-b3d7: it is the ONLY
        // ticket. The slices are what that one ticket carries — its body lists every one of them
        // and the stored set is what the run works — so the count is of slices, not of tickets.
        EpicOutcome epic =>
            $"an epic as one work ticket (`{epic.Parent.SpecId}`) "
            + $"carrying {epic.Children.Count} slice(s)",
        TicketGraphOutcome graph => $"a list of {graph.Tickets.Count} tickets, in the order they must merge", // 2026-10-14-dbcel
        TicketCommentOutcome => "a comment on the ticket",
        _ => throw new InvalidOperationException(
            $"Outcome kind '{proposal.GetType().Name}' has no summary shape."),
    };
}
