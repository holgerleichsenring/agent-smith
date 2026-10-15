using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-14-dbcel: the TICKET-TEXT APPROVAL RECORD of one ticket of a filed list — an empty set carrying the
/// approval and the ticket's title as its goal, flagged <see cref="SpecApprovalRecord.TicketText"/> so the run derives
/// the specs. Stored under the ticket's key like a filed spec's record, so routing, discovery, the run's repository
/// scope, the ApprovedSeries row and the cited material work exactly as they do for one. Stored LAST: its existence is
/// the mark that the ticket's filing is complete.
/// </summary>
public sealed class GraphTicketApproval(
    ISpecApprovalStore store,
    IReferenceSetReader references,
    SeriesRepos placement,
    TimeProvider time,
    ILogger<GraphTicketApproval> logger)
{
    private readonly ApprovalCitations _citations = new(store, references);

    /// <summary>The stored record, citing every set and image the conversation holds.</summary>
    public async Task<SpecApprovalRecord> RecordAsync(
        ConversationState state, ResolvedProject project, string ticketId, GraphTicket ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(ticket);
        var key = ApprovedSpecSetRecorder.KeyFor(project, ticketId).Value;
        var approval = new SpecApproval(time.GetUtcNow(), state.JobId, state.UserId);
        var set = new SpecSet(key, [], SpecAccounting.Empty, [], SpecSource.Approved, Approval: approval) { Goal = ticket.Title };
        // The ticket's own repositories, else the conversation's (the recorder's rule); the carrier is the only one, else none.
        IReadOnlyList<string> repositories = ticket.Repos.Count > 0
            ? ticket.Repos : ApprovedSpecSetRecorder.Repositories(state, project, basis: null);
        var uncited = new SpecApprovalRecord(key, set, repositories, project.Tracker.Name,
            placement.CarrierOf([], repositories), ticketId) { Project = project.Name, TicketText = true };
        var record = await _citations.SaveAsync(await _citations.CiteAsync(uncited, state.JobId, ct), state.JobId, ct);
        logger.LogInformation(
            "Ticket-text approval {Key} stored from conversation {Conversation}: repositories {Repos}, citing {Sets} set(s) "
            + "and {Images} image(s)", key, state.JobId, string.Join(", ", repositories), record.CitedSets.Count, record.CitedImages.Count);
        return record;
    }
}
