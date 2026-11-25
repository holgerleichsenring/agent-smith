using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-25-0873: what a project's setup says about the identity its tracker writes run under — advisory, as
/// <see cref="ProjectMachineFindings"/> is for repositories. A tracker on GitHub, GitLab or Azure Boards whose hoster the
/// project's block does not name writes with the tracker's own token. A tracker that projects write to under different
/// identities cannot tell their moves apart: each project knows only its own identity as itself, so another project's
/// moves read as a person's.
/// </summary>
public static class TrackerMachineFindings
{
    /// <summary>The finding for a tracker whose hoster the project's block does not name; none for Jira.</summary>
    public static IEnumerable<StartupFinding> Missing(string project, MachineIdentityConfig? machine, string tracker, TrackerType type)
    {
        if (MachineIdentityConfig.HosterOf(type) is not { } hoster || machine?.For(hoster) is not null) yield break;
        yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
            $"Project '{project}' has no machine identity for {type}: agent-smith comments on and moves tickets in tracker "
            + $"'{tracker}' with the tracker's own token, as whoever owns it. Set machine.{ProjectMachineFindings.Key(hoster)} in the project's settings.",
            project, Field: "machine");
    }

    /// <summary>Every resolved project's tracker finding, and one per project of a tracker written under several identities.</summary>
    public static IEnumerable<StartupFinding> For(IReadOnlyCollection<ResolvedProject> projects) =>
        [.. projects.SelectMany(p => Missing(p.Name, p.Machine, p.Tracker.Name, p.Tracker.Type)), .. Shared(projects)];

    private static IEnumerable<StartupFinding> Shared(IEnumerable<ResolvedProject> projects)
    {
        foreach (var group in projects.Where(p => MachineIdentityConfig.HosterOf(p.Tracker.Type) is not null).GroupBy(p => p.Tracker.Name))
        {
            var identities = group.Select(p => (p.Name, Identity: Said(p.Tracker))).ToList();
            if (identities.Select(i => i.Identity).Distinct(StringComparer.Ordinal).Count() < 2) continue;
            var who = string.Join(", ", identities.Select(i => $"{i.Name}: {i.Identity}"));
            foreach (var (name, _) in identities)
                yield return new StartupFinding(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
                    $"Tracker '{group.Key}' is written to under different identities ({who}). Each project tells its own moves "
                    + "apart only by its own identity, so another project's moves read as a person's. Give these projects the same "
                    + "machine identity for this hoster.", name, Field: "machine");
        }
    }

    private static string Said(TrackerConnection tracker) =>
        tracker.Machine is { } machine ? $"machine identity '{machine.Name}' (secret '{machine.Auth}')" : "the tracker's own token";
}
