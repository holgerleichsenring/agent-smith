using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
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
/// <para>
/// 2026-09-27-1bd9: and only projects ON THE TRACKER THAT ANSWERED. The match keeps a project
/// whose trigger KIND equals the ticket's platform, and two Jira trackers are both "jira" — so a
/// ticket swept up from the first tracker holding its number could be matched to a project routed
/// to the second, and the binding, which re-fetches by id on THAT project's tracker, would bind a
/// different board's ticket of the same number. A project matched elsewhere is reported rather
/// than dropped in silence: it is the reason a ticket that looked answerable is being asked about.
/// </para>
/// </summary>
public sealed class TicketProjectChoice(
    ITicketProviderFactory providers,
    ILogger<TicketProjectChoice> logger)
{
    public async Task<TicketProjectAnswer?> ForAsync(
        AgentSmithConfig config, string ticketId, CancellationToken ct) =>
        (await LookupAsync(config, ticketId, ct)).Answer;

    /// <summary>
    /// 2026-09-27-481bb: the same sweep, and the trackers it could not ASK. A tracker that has no
    /// such ticket and one that could not be reached both simply do not answer, so a caller told
    /// only "nobody has it" cannot tell a board without the ticket from a board it never saw.
    /// <para>
    /// The list is bounded by the sweep's own shape: it returns on the first tracker that answers,
    /// so it holds the trackers asked BEFORE that one and no others.
    /// </para>
    /// </summary>
    public async Task<TicketProjectLookup> LookupAsync(
        AgentSmithConfig config, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var unreachable = new List<string>();
        foreach (var tracker in config.Trackers.Values)
        {
            var platform = tracker.Type.ToString().ToLowerInvariant();
            var (ticket, reachable) = await TryReadAsync(tracker, ticketId, ct);
            if (ticket is null)
            {
                if (!reachable) unreachable.Add(tracker.Name);
                continue;
            }

            var binding = TicketBinding.For(
                tracker.Name, platform, ticket.Id.Value, ticket.Title, ticket.Labels);
            var matches = TicketProjectMatch.Of(config, binding.Envelope(platform));
            var here = TrackerProjects.RoutedTo(config, tracker.Name);
            return new TicketProjectLookup(
                new TicketProjectAnswer(
                    binding,
                    [.. matches.Matched.Intersect(here, StringComparer.Ordinal)],
                    [.. matches.Unanswerable.Intersect(here, StringComparer.Ordinal)],
                    [.. matches.Matched.Except(here, StringComparer.Ordinal)]),
                unreachable);
        }

        return new TicketProjectLookup(null, unreachable);
    }

    // The next tracker is asked either way; what the second value carries is WHY this one said
    // nothing — a board without the ticket, or a board we never reached.
    private async Task<(Domain.Entities.Ticket? Ticket, bool Reachable)> TryReadAsync(
        TrackerConnection tracker, string ticketId, CancellationToken ct)
    {
        try
        {
            return (await providers.Create(tracker).GetTicketAsync(new TicketId(ticketId.Trim()), ct), true);
        }
        catch (TicketNotFoundException)
        {
            return (null, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex,
                "Tracker {Tracker} could not be asked for ticket {Ticket}", tracker.Name, ticketId);
            return (null, false);
        }
    }
}

/// <summary>The ticket, the projects it names, and the ones a ticket cannot answer for.</summary>
/// <param name="Elsewhere">2026-09-27-1bd9: projects this ticket's labels DO name, on other
/// trackers — which cannot hold it. Carried so the person is told why they are being asked about a
/// ticket whose routing looks unambiguous, rather than left to conclude the labels are wrong.</param>
