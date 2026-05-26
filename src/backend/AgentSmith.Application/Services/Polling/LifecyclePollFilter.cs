using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Filters discovery candidates down to those that are actually claimable.
/// A ticket is claimable if it has no lifecycle label, or only the Pending one —
/// i.e. it's a brand-new arrival or a Pending catchup. Tickets already in
/// Enqueued/InProgress/Done/Failed are excluded so a single poll cycle can union
/// the discovery and Pending-catchup queries without producing duplicate claims
/// or stomping on in-flight work. Operator-defined labels that share the
/// `agent-smith:` prefix (e.g. `agent-smith:init`) are ignored — only the five
/// known lifecycle statuses gate claimability.
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
                case TicketLifecycleStatus.Pending:
                    continue;
                case TicketLifecycleStatus.Enqueued:
                case TicketLifecycleStatus.InProgress:
                case TicketLifecycleStatus.Done:
                case TicketLifecycleStatus.Failed:
                    return false;
            }
        }
        return true;
    }

    public static IEnumerable<Ticket> KeepClaimable(IEnumerable<Ticket> tickets)
        => tickets.Where(t => IsClaimableLifecycle(t.Labels));
}
