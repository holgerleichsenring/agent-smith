using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-17-023fb, extracted 2026-11-24-297b: whether a ticket's pull request may be refreshed by a refresh-base run —
/// not closed, no run of its own (a running one merges the base at its own checkout), none queued, not waiting for an
/// answer. The landing refresh and the conflict refresh ask the same.
/// </summary>
public sealed class RefreshableTicket(IActiveRunLease leases, IPreviousAttemptReader attempts, ITicketClosedMarkStore closed)
{
    public async Task<bool> FreeAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var key = TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        if ((await closed.OfAsync(project.Tracker.Name, [key], ct)).Count > 0) return false;
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        if (await attempts.HasQueuedAsync(project.Name, ticketId, ct)) return false;
        return (await attempts.LatestAsync(project.Name, ticketId, null, ct))?.Status != RunStatuses.WaitingForInput;
    }
}
