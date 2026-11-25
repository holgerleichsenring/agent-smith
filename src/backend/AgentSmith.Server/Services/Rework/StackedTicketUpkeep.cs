using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-9a39c: what every nudge of a ticket with an attempt does for a stack, before the attempt path: its green
/// attempt wakes the tickets stacked on it (2026-11-17-9a39a), and its own pull requests leave a prerequisite's branch
/// once that prerequisite landed. 2026-11-25-9992e: a ticket an active series holds is retargeted by its series master
/// (retarget_pull_request), never here; its wake stays.
/// </summary>
public sealed class StackedTicketUpkeep(StackedDependentsWake wake, StackedPullRequestRetarget retarget, ISeriesRunStore series)
{
    public async Task KeepAsync(ResolvedProject project, string ticketId, PreviousAttempt attempt, CancellationToken ct)
    {
        await wake.WakeAsync(project, ticketId, attempt, ct);
        if (!await series.HoldsTicketAsync(project.Name, ticketId, ct)) await retarget.RetargetAsync(project, ticketId, attempt, ct);
    }
}
