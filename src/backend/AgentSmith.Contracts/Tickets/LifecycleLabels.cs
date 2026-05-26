using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// Closed set of labels that agent-smith owns to represent ticket lifecycle status.
/// The `agent-smith:` prefix is a hint that the label belongs to this framework, but
/// `IsLifecycleLabel` only returns true for labels matching one of the five known
/// statuses — operator-defined labels that share the prefix (e.g. `agent-smith:init`
/// in `pipeline_from_label`) are preserved through transitions and pass the
/// resolution filter unchanged.
/// </summary>
public static class LifecycleLabels
{
    public const string Prefix = "agent-smith:";

    public static string For(TicketLifecycleStatus status) => status switch
    {
        TicketLifecycleStatus.Pending => Prefix + "pending",
        TicketLifecycleStatus.Enqueued => Prefix + "enqueued",
        TicketLifecycleStatus.InProgress => Prefix + "in-progress",
        TicketLifecycleStatus.Done => Prefix + "done",
        TicketLifecycleStatus.Failed => Prefix + "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static bool IsLifecycleLabel(string label)
        => TryParse(label, out _);

    public static bool TryParse(string label, out TicketLifecycleStatus status)
    {
        status = default;
        if (label is null || !label.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var suffix = label[Prefix.Length..];
        switch (suffix)
        {
            case "pending": status = TicketLifecycleStatus.Pending; return true;
            case "enqueued": status = TicketLifecycleStatus.Enqueued; return true;
            case "in-progress": status = TicketLifecycleStatus.InProgress; return true;
            case "done": status = TicketLifecycleStatus.Done; return true;
            case "failed": status = TicketLifecycleStatus.Failed; return true;
            default: return false;
        }
    }
}
