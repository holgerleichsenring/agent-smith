using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Optional per-tracker map from an agent-smith lifecycle state to the operator's
/// native Jira workflow status name. A non-empty map switches the Jira transitioner
/// from label mode to native mode. Unmapped states fall back to labels, so an
/// operator can drive only the states their workflow models (e.g. In Progress + Done)
/// natively and leave the rest as labels. Empty (default) → label mode, unchanged.
/// </summary>
public sealed record JiraLifecycleStatusMap(IReadOnlyDictionary<TicketLifecycleStatus, string> Names)
{
    public static JiraLifecycleStatusMap Empty { get; } =
        new(new Dictionary<TicketLifecycleStatus, string>());

    public bool IsEmpty => Names.Count == 0;

    public bool TryNameFor(TicketLifecycleStatus status, out string name)
    {
        if (Names.TryGetValue(status, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
        {
            name = mapped;
            return true;
        }
        name = string.Empty;
        return false;
    }
}
