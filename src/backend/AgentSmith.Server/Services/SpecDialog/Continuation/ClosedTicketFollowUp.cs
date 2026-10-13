using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: an approval in the conversation of a ticket a PERSON closed files one follow-up
/// ticket — the proposal's drafts minus what already ran on the closed ticket — through the ordinary
/// filer, and links the two. The conversation stays the closed ticket's; a later approval there files
/// another follow-up.
/// </summary>
public sealed class ClosedTicketFollowUp(OutcomeTicketFiler filer, FollowUpLink link)
{
    public async Task<TicketContinuationOutcome> FileAsync(
        ConversationState state, OutcomeProposal proposal, TicketContinuationDecision decision,
        ResolvedProject project, string ticketId, bool mayStartRuns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Drafts.Count == 0) return new(decision.Reason, Acted: true);
        OutcomeProposal left = proposal is EpicOutcome epic && decision.Drafts.Count > 1
            ? epic with { Children = decision.Drafts }
            : new SpecOutcome(decision.Drafts[0]);
        var report = await filer.FileAsync(state, left, mayStartRuns, ct);
        if (report.Filed.Count == 0)
            return new($"Ticket {ticketId} was closed by a person, and filing its follow-up failed: {report.Error}. "
                + "Approve again once the tracker answers.", Acted: false, report, left);
        var missed = await link.LinkAsync(project, ticketId, report.Filed[0], ct);
        return new($"{decision.Reason} Filed: {string.Join(", ", report.Filed.Select(f => f.Key ?? f.Reference))}."
            + (report.Error is { } error ? $" Filing did not complete: {error}." : string.Empty)
            + (missed is null ? string.Empty : $" {missed}"), Acted: true, report, left);
    }
}
