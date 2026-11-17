using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-9a39c: what every nudge of a ticket with an attempt does for a stack, before the attempt path: its green
/// attempt wakes the tickets stacked on it (2026-11-17-9a39a), and its own pull requests leave a prerequisite's branch
/// once that prerequisite landed.
/// </summary>
public sealed class StackedTicketUpkeep(StackedDependentsWake wake, StackedPullRequestRetarget retarget)
{
    public async Task KeepAsync(ResolvedProject project, string ticketId, PreviousAttempt attempt, CancellationToken ct)
    {
        await wake.WakeAsync(project, ticketId, attempt, ct);
        await retarget.RetargetAsync(project, ticketId, attempt, ct);
    }
}
