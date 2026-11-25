using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-25-0873: every advisory a configuration carries about who acts as the machine — a project's repositories
/// (<see cref="ProjectMachineFindings"/>), its tracker (<see cref="TrackerMachineFindings"/>) and the PoC targets
/// (<see cref="PocMachineFindings"/>) — gathered once for the materializer and the studio draft.
/// </summary>
public static class MachineAdvisories
{
    public static IEnumerable<StartupFinding> Of(AgentSmithConfig config) =>
        [.. ProjectMachineFindings.Missing(config.Projects.Values), .. TrackerMachineFindings.For([.. config.Projects.Values]),
            .. PocMachineFindings.Missing(config)];

    /// <summary>A project draft's tracker finding; none before its tracker resolves.</summary>
    public static IEnumerable<StartupFinding> Tracker(ProjectEntity draft, RawProjectEntry project, RawTrackerEntry? tracker) =>
        tracker is null ? [] : TrackerMachineFindings.Missing(draft.Id, project.Machine, draft.Tracker, tracker.Type);
}
