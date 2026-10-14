using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042eg: filing created the ticket; this is what makes a run pick it up. It resolves
/// the ticket the way the POLLER would — the envelope the poller builds carries labels, ticket id
/// and platform and nothing else — and moves it into a trigger status only when that resolution
/// names the filing project.
/// <para>
/// RESOLVE FIRST, MOVE ONLY WHAT RESOLVES. A ticket nothing would route gains nothing from a move:
/// it would sit in a trigger status no poll ever claims, and reporting it started would be false.
/// So it reports NOT STARTED and says WHICH of the resolver's three silent drops happened — a
/// blocking startup finding, a resolution the ticket does not satisfy, or the project's own
/// pipeline rules — because those want three different things done about them.
/// </para>
/// <para>2026-09-20-2ba8: the routing tag goes on FIRST, through <see cref="FiledWorkTagger"/> —
/// here, where the start already is and the approved set is therefore already stored — and the
/// envelope is resolved against the labels tagging actually left on the ticket.</para>
/// <para>2026-10-13-9a59m: the resolution and the move are <see cref="FiledWorkMove"/>'s.</para>
/// <para>
/// MOVING A TICKET STARTS A RUN, so it needs runs.control. Where the created status already
/// triggers, the ticket starts as it always did — the permission governs the MOVE and nothing
/// else. Without it the reason names a move in the TRACKER rather than a wait: the bool is
/// sampled once, on the request that opened the turn, and no later path re-reads it.
/// </para>
/// </summary>
public sealed class FiledWorkStarter(
    FiledWorkTagger tagger,
    FiledWorkMove move,
    ILogger<FiledWorkStarter> logger)
{
    /// <summary>Tags the ticket, starts it if it can be started, and stamps what happened onto its
    /// entry in <paramref name="filed"/>. Never throws: a tracker that refuses the tag or the move
    /// is a reason on the report, and the rest of the filing carries on.</summary>
    public async Task StampAsync(
        ITicketProvider provider, ResolvedProject project, CreatedTicket ticket,
        IReadOnlyList<string> labels, bool mayStartRuns, List<FiledTicket> filed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(filed);
        var tagged = await tagger.ApplyAsync(provider, project, ticket, labels, cancellationToken);
        var start = tagged.Explaining(await move.StartAsync(
            provider, project, ticket.Id, tagged.Labels, mayStartRuns, cancellationToken));
        logger.LogInformation(
            "Filed ticket {Ticket} is {State}: {Reason}", ticket.Id.Value, start.State, start.Reason);
        var at = filed.FindIndex(t => t.TicketId == ticket.Id.Value);
        if (at >= 0) filed[at] = filed[at] with { Start = start };
    }
}
