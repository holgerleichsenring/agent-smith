using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-14-dbceh: a RequirementsMet nudge on a ticket no attempt worked, under the worker's lock on it. It
/// starts the ticket only when the gate would admit it — every requirement landed, the filing complete — and
/// its approver could start runs, and never one a person marked done by hand or closed: tagged or moved as
/// a filed ticket is, then claimed now through the poller's own route, since in webhook mode nothing else
/// delivers a ticket that already sits in a trigger status. A thrown exception reschedules the nudge.
/// 2026-11-17-e061b: the tag and the move run as a 'start' pipeline (<see cref="Acts.TicketStartRun"/>) with a run id.
/// 2026-11-19-1b72a: the start itself is <see cref="WaitingTicketStart"/>, which a series' resume shares.
/// </summary>
public sealed class RequirementsMetStart(WaitingTicketStart start)
{
    public async Task<ReworkNudgeDisposition> StartAsync(ResolvedProject project, ClaimedReworkNudge nudge, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(nudge);
        await start.StartAsync(project, nudge.TicketId, onlyWithRequirements: true, ct);
        return ReworkNudgeDisposition.Finish;
    }
}
