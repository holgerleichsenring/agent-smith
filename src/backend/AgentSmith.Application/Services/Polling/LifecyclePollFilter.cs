using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Filters discovery candidates down to those that are actually claimable.
/// p0261: the Done/Failed lifecycle tags are NO LONGER decisive for triggering —
/// they are human-facing status markers only. Triggering rests on the native ticket
/// status (the poller's trigger_statuses gate, TrackerPoller.IsStatusAllowed) plus the
/// run lease for concurrency (the sole single-run guard since p0251/p0252). So a
/// reopened ticket still carrying a stale agent-smith:done/failed tag is claimable
/// again; a terminalized ticket is kept out by its NATIVE status, not the tag.
/// Only the in-flight tags (Enqueued/InProgress) still gate here, as a belt over the
/// lease — and even that is already partly redundant post-p0252 (moving in-flight
/// gating fully onto the lease is p0262). Operator-defined `agent-smith:`-prefixed
/// labels (e.g. `agent-smith:init`) are ignored — only lifecycle statuses are read.
/// </summary>
public static class LifecyclePollFilter
{
    public static bool IsClaimableLifecycle(IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            if (!LifecycleLabels.TryParse(label, out var status)) continue;
            switch (status)
            {
                // p0261: Pending / Done / Failed are non-blocking markers — a
                // brand-new arrival, a Pending catchup, or a re-opened ticket whose
                // prior terminal tag is now stale. The native status decides instead.
                case TicketLifecycleStatus.Pending:
                case TicketLifecycleStatus.Done:
                case TicketLifecycleStatus.Failed:
                    continue;
                case TicketLifecycleStatus.Enqueued:
                case TicketLifecycleStatus.InProgress:
                    return false;
            }
        }
        return true;
    }

    public static IEnumerable<Ticket> KeepClaimable(IEnumerable<Ticket> tickets)
        => tickets.Where(t => IsClaimableLifecycle(t.Labels));
}
