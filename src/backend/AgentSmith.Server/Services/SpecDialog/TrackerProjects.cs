using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-1bd9: which configured projects are routed to one tracker.
/// <para>
/// One place, because two readers were about to keep their own copy and then disagree: the sweep
/// behind the dialog's ticket field already intersected with it, and the by-id choice now narrows
/// by it. A project is bound to a ticket by re-fetching that ticket on the PROJECT'S tracker, so
/// a project routed elsewhere can never be the project of this ticket, whatever its labels say.
/// </para>
/// </summary>
internal static class TrackerProjects
{
    public static IReadOnlyList<string> RoutedTo(AgentSmithConfig config, string tracker) =>
        [.. config.Projects
            .Where(p => string.Equals(p.Value.Tracker.Name, tracker, StringComparison.Ordinal))
            .Select(p => p.Key)];
}
