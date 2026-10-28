using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;

namespace AgentSmith.Application.Services.Triggers;

/// <summary>
/// 2026-10-28-7a69a: a Jira ticket belongs to the projects whose tracker holds its Jira project.
/// <para>
/// Labels say nothing about the site's project, so two trackers on one site — one per Jira project —
/// both matched a webhook whose labels fit, and each spawned a run. The key carries the project
/// (<c>BET-12</c>), so a project routed to a tracker keyed <c>ALP</c> does not match it. A tracker
/// without a key, or an id without a project part, holds every project, as it reads them.
/// </para>
/// </summary>
public static class JiraProjectScope
{
    public static bool Holds(ResolvedProject project, IncomingTicketEnvelope envelope)
    {
        if (project.Tracker.Type != TrackerType.Jira || string.IsNullOrWhiteSpace(project.Tracker.Project)) return true;
        if (!string.Equals(envelope.Platform, "jira", StringComparison.OrdinalIgnoreCase)) return true;
        var key = envelope.TicketId ?? string.Empty;
        var dash = key.LastIndexOf('-');
        return dash <= 0 || string.Equals(key[..dash], project.Tracker.Project, StringComparison.OrdinalIgnoreCase);
    }
}
