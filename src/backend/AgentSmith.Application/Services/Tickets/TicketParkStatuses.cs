using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59k: the statuses agent-smith itself moves a project's tickets into, from the project's
/// effective trigger, then its tracker — the ONE place that precedence lives. Done is the trigger's
/// done_status; Failed its failed_status, else Done, as the spawn seeds it; Clarification and
/// NotImplementable the trigger's, else the tracker's, NotImplementable falling back to Clarification
/// like the hand-back park. A blank configured value is no status.
/// </summary>
public sealed record TicketParkStatuses(string? Done, string? Failed, string? Clarification, string? NotImplementable)
{
    public static TicketParkStatuses Of(ResolvedProject project)
    {
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        var done = Either(trigger?.DoneStatus, null);
        var clarification = Either(trigger?.NeedsClarificationStatus, project.Tracker.NeedsClarificationStatus);
        return new TicketParkStatuses(
            done,
            trigger is null ? null : Either(trigger.FailedStatus, done),
            clarification,
            Either(Either(trigger?.NotImplementableStatus, project.Tracker.NotImplementableStatus), clarification));
    }

    /// <summary>Whether <paramref name="status"/> is any status agent-smith sets.</summary>
    public bool Own(string? status) => IsRunEnd(status) || IsHandbackPark(status);

    /// <summary>Whether <paramref name="status"/> is where a finished run leaves the ticket: done or failed.</summary>
    public bool IsRunEnd(string? status) => Same(status, Done) || Same(status, Failed);

    /// <summary>Whether <paramref name="status"/> is where a hand-back parks the ticket.</summary>
    public bool IsHandbackPark(string? status) => Same(status, Clarification) || Same(status, NotImplementable);

    private static string? Either(string? own, string? fallback) => string.IsNullOrWhiteSpace(own) ? fallback : own;

    private static bool Same(string? status, string? configured) =>
        !string.IsNullOrWhiteSpace(configured) && string.Equals(status, configured, StringComparison.OrdinalIgnoreCase);
}
