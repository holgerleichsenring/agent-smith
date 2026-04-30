using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Filters discovery candidates down to those that are actually claimable.
/// A ticket is claimable if it has no lifecycle label, or only the Pending one —
/// i.e. it's a brand-new arrival or a Pending catchup. Tickets already in
/// Enqueued/InProgress/Done/Failed are excluded so a single poll cycle can union
/// the discovery and Pending-catchup queries without producing duplicate claims
/// or stomping on in-flight work.
/// </summary>
public static class LifecyclePollFilter
{
    private const string LifecyclePrefix = "agent-smith:";

    public static bool IsClaimableLifecycle(IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            if (!label.StartsWith(LifecyclePrefix, StringComparison.Ordinal)) continue;
            var suffix = label[LifecyclePrefix.Length..];
            switch (suffix)
            {
                case "pending":
                    continue;
                case "enqueued":
                case "in-progress":
                case "done":
                case "failed":
                    return false;
            }
        }
        return true;
    }

    public static IEnumerable<Ticket> KeepClaimable(IEnumerable<Ticket> tickets)
        => tickets.Where(t => IsClaimableLifecycle(t.Labels));
}
