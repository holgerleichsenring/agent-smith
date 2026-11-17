using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.SpecDialog.RunMessages;

/// <summary>
/// 2026-11-17-4860b: the code attempt working a ticket right now — the run whose id the ticket's lease carries and that
/// is the ticket's newest code attempt, in any project sharing the ticket's tracker. A claim whose run has not started,
/// or a lease another kind of run holds, is no attempt a message can reach.
/// </summary>
public sealed class TicketRunningAttempt(
    FiledWorkTrackerProjects projects, IActiveRunLease leases, IPreviousAttemptReader attempts)
{
    /// <summary>The running attempt's id, or null when no code attempt works the ticket.</summary>
    public async Task<string?> RunIdAsync(string project, string ticketId, CancellationToken ct)
    {
        foreach (var sibling in projects.SharingTrackerWith(project))
        {
            var lease = await leases.GetByTicketAsync(sibling.Name, new TicketId(ticketId), ct);
            if (lease?.RunId is not { } run) continue;
            var newest = await attempts.LatestAsync(sibling.Name, ticketId, null, ct);
            if (string.Equals(newest?.RunId, run, StringComparison.Ordinal)) return run;
        }
        return null;
    }
}
