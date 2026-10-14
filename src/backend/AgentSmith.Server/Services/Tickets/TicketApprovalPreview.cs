using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets.Conversation;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59l: the approval preview — <see cref="TicketContinuation.Decide"/> over the ticket's
/// conversation's open proposal and the state the approval would read. Null when the project is unknown,
/// the ticket has no conversation, or the conversation holds no open proposal.
/// </summary>
public sealed class TicketApprovalPreview(
    TicketProjects projects, TicketTranscriptReader transcript, TicketContinuationStateReader reader,
    Application.Services.Specs.ISeriesSettingsReader series)
{
    /// <param name="mayStartRuns">2026-10-13-9a59m: whether the caller holds runs.control — the start arm differs.</param>
    public async Task<TicketApprovalPreviewRead?> ReadAsync(string project, string ticketId, bool mayStartRuns, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        if (await transcript.SessionAsync(resolved, ticketId, ct) is not { LatestProposalJson: { } json }) return null;
        if (Proposal(json) is not { } proposal) return null;
        var read = await reader.ReadAsync(project, ticketId, mayStartRuns, ct);
        if (read.State is not { } state) return new(null, read.Failure);
        var decision = TicketContinuation.Decide(state, proposal, series.Current());
        return new(new TicketApprovalPreviewView(project, ticketId, decision.Effect, decision.Reason,
            decision.Executed, decision.Unstarted, decision.BasisRunId, decision.Specs, decision.Start), null);
    }

    // An unreadable stored proposal is no proposal to preview, as the conversation read shows it absent.
    private static OutcomeProposal? Proposal(string json)
    {
        try { return OutcomeProposalJson.Read(json); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or NotSupportedException) { return null; }
    }
}

/// <summary>The preview, or why the state behind it could not be read.</summary>
public sealed record TicketApprovalPreviewRead(TicketApprovalPreviewView? View, string? Failure);
