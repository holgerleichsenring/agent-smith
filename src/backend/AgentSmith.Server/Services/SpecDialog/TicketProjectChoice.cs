using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51a: which project a ticket names, when the caller names only the ticket.
/// <para>
/// A ticket id alone says nothing about which tracker holds it, so every configured tracker is
/// ASKED — bounded by the number of trackers, which is a handful — and the ones that have it are
/// matched against the projects routed to them. The first tracker that answers wins its own
/// match set; a ticket number that exists on two trackers is different work, so each tracker's
/// answer stays its own.
/// </para>
/// <para>
/// The result SAYS WHY, in all three shapes: one project named, several to choose between, and
/// none — with the projects that could not be answered from a ticket at all listed separately,
/// because a project routed by area path or repository is not the operator's configuration being
/// wrong.
/// </para>
/// </summary>
public sealed class TicketProjectChoice(
    ITicketProviderFactory providers,
    ILogger<TicketProjectChoice> logger)
{
    public async Task<TicketProjectAnswer?> ForAsync(
        AgentSmithConfig config, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        foreach (var tracker in config.Trackers.Values)
        {
            var platform = tracker.Type.ToString().ToLowerInvariant();
            var ticket = await TryReadAsync(tracker, ticketId, ct);
            if (ticket is null) continue;
            var binding = TicketBinding.For(
                tracker.Name, platform, ticket.Id.Value, ticket.Title, ticket.Labels);
            var matches = TicketProjectMatch.Of(config, binding.Envelope(platform));
            return new TicketProjectAnswer(binding, matches.Matched, matches.Unanswerable);
        }

        return null;
    }

    private async Task<Domain.Entities.Ticket?> TryReadAsync(
        TrackerConnection tracker, string ticketId, CancellationToken ct)
    {
        try
        {
            return await providers.Create(tracker).GetTicketAsync(new TicketId(ticketId.Trim()), ct);
        }
        // A tracker that does not have this ticket, or cannot be reached, simply does not answer
        // for it — the next one is asked, and a ticket nobody has is reported as not found.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex,
                "Tracker {Tracker} did not answer for ticket {Ticket}", tracker.Name, ticketId);
            return null;
        }
    }
}

/// <summary>The ticket, the projects it names, and the ones a ticket cannot answer for.</summary>
public sealed record TicketProjectAnswer(
    TicketBinding Binding,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Unanswerable);
