using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Default lifecycle labels used until p95c replaces them with LifecycleConfig-driven values.
/// The "agent-smith:" prefix identifies labels owned by this framework; any label with the prefix
/// that is not a known lifecycle status is considered orphaned and skipped when rebuilding label sets.
/// </summary>
internal static class LifecycleLabels
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
        => label.StartsWith(Prefix, StringComparison.Ordinal);

    public static bool TryParse(string label, out TicketLifecycleStatus status)
    {
        status = default;
        if (!IsLifecycleLabel(label)) return false;

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
