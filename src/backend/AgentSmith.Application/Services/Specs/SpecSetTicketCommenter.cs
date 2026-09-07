using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: posts the derived cut back to the ticket as "this is how I understood it",
/// and the run PROCEEDS.
/// <para>
/// Ratification is non-blocking on purpose: blocking would reintroduce exactly the wait
/// that Approval was deleted for, and a correcting comment is the re-run path anyway. The
/// risk this accepts is that a wrong reading gets built before anyone objects; it is
/// bounded by the pull request and by the discarded list being visible in both places.
/// </para>
/// <para>
/// The comment is an OUTPUT, never an input: the next run reads the branch artifact, not
/// this comment, or it would be feeding on its own echo. Its body is
/// <see cref="SpecSetComment"/>'s — criteria, facts and assumptions per phase.
/// </para>
/// </summary>
public sealed class SpecSetTicketCommenter(
    ITicketProviderFactory ticketFactory,
    ILogger<SpecSetTicketCommenter> logger)
{
    public async Task PostAsync(
        PipelineContext pipeline, TrackerConnection? tracker, SpecSet set, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (tracker is null || set.Phases.Count == 0) return;
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return;
        // An inline ticket exists only on this run — there is nothing to comment on.
        if (pipeline.Has(ContextKeys.InlineTicket)) return;

        try
        {
            var url = pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var u) ? u : null;
            await ticketFactory.Create(tracker).UpdateStatusAsync(
                ticket.Id, SpecSetComment.Render(set, url), ct);
            logger.LogInformation(
                "Posted the derived cut of {Key} ({Phases} phase(s)) to ticket {Ticket}",
                set.Key, set.Phases.Count, ticket.Id.Value);
        }
        catch (Exception ex)
        {
            // The artifacts are on the branch and in the pull request either way; a tracker
            // that refuses a comment must not end a run that is otherwise fine.
            logger.LogWarning(ex, "Could not post the derived cut to ticket {Ticket}", ticket.Id.Value);
        }
    }
}
