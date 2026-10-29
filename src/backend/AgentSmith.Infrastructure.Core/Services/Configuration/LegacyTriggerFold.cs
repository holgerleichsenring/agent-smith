using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-29-6bd3a: moves every per-project trigger block into what the studio shows — the project's
/// resolution and its tracker — and clears it. A block value is taken only where nothing is set: where
/// both say something, the visible value is the one the operator chose, and it is the one that runs.
/// <para>
/// Projects are folded in name order, so the first project of a tracker fills a field and a second
/// one with a different value for it is a finding rather than a silent overwrite. A terminal status
/// that is one of the tracker's trigger statuses is not taken: on the tracker it would re-claim every
/// ticket it parks, for every project of that tracker.
/// </para>
/// </summary>
public sealed class LegacyTriggerFold
{
    public IReadOnlyList<StartupFinding> Apply(RawAgentSmithConfig raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var findings = new List<StartupFinding>();
        var trackers = ConfigNames.KeyedByName(raw.Trackers);
        var filledBy = new Dictionary<(RawTrackerEntry, string), (string Project, string Value)>();
        foreach (var (name, project) in raw.Projects.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            trackers.TryGetValue(project.Tracker, out var tracker);
            foreach (var (kind, type, block) in Blocks(project))
            {
                if (tracker is null || tracker.Type != type)
                {
                    findings.Add(Dropped(name, kind, project.Tracker));
                    continue;
                }
                FoldResolution(project, block);
                new LegacyTriggerTrackerFill(name, kind, tracker, filledBy, findings).Apply(block);
            }
            project.LegacyJiraTrigger = null;
            project.LegacyGithubTrigger = null;
            project.LegacyGitlabTrigger = null;
            project.LegacyAzuredevopsTrigger = null;
        }
        return findings;
    }

    private static IEnumerable<(string Kind, TrackerType Type, LegacyTriggerBlock Block)> Blocks(RawProjectEntry p)
    {
        if (p.LegacyJiraTrigger is { } jira) yield return ("jira_trigger", TrackerType.Jira, jira);
        if (p.LegacyGithubTrigger is { } github) yield return ("github_trigger", TrackerType.GitHub, github);
        if (p.LegacyGitlabTrigger is { } gitlab) yield return ("gitlab_trigger", TrackerType.GitLab, gitlab);
        if (p.LegacyAzuredevopsTrigger is { } azdo) yield return ("azuredevops_trigger", TrackerType.AzureDevOps, azdo);
    }

    private static void FoldResolution(RawProjectEntry project, LegacyTriggerBlock block)
    {
        if (project.Resolution is { Count: > 0 } || block.ProjectResolution is not { } resolution) return;
        project.Resolution = new Dictionary<string, string>
        {
            [ConfigStudioCapabilities.WireName(resolution.Strategy)] = resolution.Value,
        };
    }

    private static StartupFinding Dropped(string project, string kind, string tracker) => new(
        StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
        $"Project '{project}' carries a {kind} block that does not match the type of its tracker "
        + $"'{tracker}', so it never ran; it is dropped.",
        project, kind, $"projects.{project}.{kind}");
}
