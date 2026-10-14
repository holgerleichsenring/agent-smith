using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>
/// 2026-10-13-9a59h: what the tracker says about a ticket after an attempt — three reads: the ticket (its
/// fingerprint against the one the attempt fetched), its comments (persons' comments after the attempt's
/// cutoff) and its status history (a person's move into a trigger status after it). The cutoff is the
/// previous-attempt rule's: acts read, else start, minus the skew margin. Null when any read fails.
/// </summary>
public sealed class TicketTrackerObserver(
    ITicketProviderFactory providers, TrackerIdentity identity, ILogger<TicketTrackerObserver> logger)
{
    public async Task<TicketObservation?> ObserveAsync(
        ResolvedProject project, Run attempt, string recordedFingerprint, CancellationToken ct)
    {
        var cutoff = new PreviousAttempt(attempt.Id, attempt.Status, attempt.StartedAt, true) { ActsReadAt = attempt.ActsReadAt };
        var ticketId = new TicketId(attempt.TicketId);
        try
        {
            var provider = providers.Create(project.Tracker);
            var ticket = TicketLabelNoteStripper.Strip(await provider.GetTicketAsync(ticketId, ct));
            var comments = await provider.GetCommentsAsync(ticketId, ct);
            var acts = comments.Where(c => !OwnTicketComment.IsOurs(c) && cutoff.Precedes(c.CreatedAt))
                .Select(c => new TicketTrackerAct(TicketTrackerAct.Comment, c.Author, c.CreatedAt)).ToList();
            if (await StatusMoveAsync(project, provider, ticketId, ct) is { } move && cutoff.Precedes(move.At))
                acts.Add(new TicketTrackerAct(TicketTrackerAct.StatusMove, move.Actor.Login ?? move.Actor.Id, move.At));
            return new TicketObservation(TicketTextFingerprint.Of(ticket) == recordedFingerprint, acts);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Ticket} of project {Project} could not be checked after attempt {RunId}",
                attempt.TicketId, attempt.Project, attempt.Id);
            return null;
        }
    }

    private async Task<TicketStatusMove?> StatusMoveAsync(
        ResolvedProject project, ITicketProvider provider, TicketId ticketId, CancellationToken ct)
    {
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        if (provider is not ITicketStatusHistory history || trigger?.TriggerStatuses is not { Count: > 0 } statuses) return null;
        var self = await identity.SelfAsync(project.Tracker, ct);
        return await history.NewestPersonMoveIntoAsync(ticketId, statuses, self, ct);
    }
}

/// <summary>2026-10-13-9a59h: one observation of the tracker after an attempt.</summary>
public sealed record TicketObservation(bool FingerprintUnchanged, IReadOnlyList<TicketTrackerAct> Acts);
