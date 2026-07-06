using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281b: builds the effective per-project trigger from the tracker's workflow fields
/// (trigger_statuses / done_status / failed_status / pipeline_from_label) plus the
/// project's flat <c>resolution:</c> shorthand. The tracker OWNS the workflow; a project
/// supplies only its resolution, and the trigger TYPE is inferred from the tracker type.
/// An explicit project trigger wrapper still wins field-by-field (override mode).
/// Runs on the raw config before catalog resolution, so every downstream consumer reads
/// the already-merged trigger and nothing changes past this point.
/// </summary>
public sealed class EffectiveTriggerBuilder
{
    public void Apply(string projectName, RawProjectEntry project, RawTrackerEntry? tracker)
    {
        if (tracker is null) return;

        var existing = GetTrigger(project, tracker.Type);
        if (existing is null && IsEmpty(project.Resolution)) return;

        var wrapper = existing ?? CreateWrapper(tracker.Type);
        FillResolution(projectName, wrapper, project.Resolution);
        FillWorkflow(wrapper, tracker, isFresh: existing is null);
        SetTrigger(project, tracker.Type, wrapper);
    }

    private static void FillResolution(
        string projectName, WebhookTriggerConfig wrapper, Dictionary<string, string>? shorthand)
    {
        if (wrapper.ProjectResolution is not null || IsEmpty(shorthand)) return;
        var (key, value) = First(shorthand!);
        wrapper.ProjectResolution = new ProjectResolutionConfig
        {
            Strategy = ParseStrategy(projectName, key),
            Value = value,
        };
    }

    private static void FillWorkflow(WebhookTriggerConfig wrapper, RawTrackerEntry tracker, bool isFresh)
    {
        var statuses = TrackerStatuses(tracker);
        if (statuses.Count > 0 && (isFresh || wrapper.TriggerStatuses.Count == 0))
            wrapper.TriggerStatuses = statuses;
        if (isFresh && !string.IsNullOrEmpty(tracker.DoneStatus))
            wrapper.DoneStatus = tracker.DoneStatus;
        wrapper.FailedStatus ??= tracker.FailedStatus;
        wrapper.NeedsClarificationStatus ??= tracker.NeedsClarificationStatus;
        if (IsEmpty(wrapper.PipelineFromLabel) && tracker.PipelineFromLabel is { Count: > 0 } labels)
            wrapper.PipelineFromLabel = new Dictionary<string, string>(labels);
    }

    private static List<string> TrackerStatuses(RawTrackerEntry tracker) =>
        tracker.TriggerStatuses.Count > 0 ? tracker.TriggerStatuses
        : tracker.OpenStates.Count > 0 ? tracker.OpenStates
        : [];

    private static WebhookTriggerConfig? GetTrigger(RawProjectEntry p, TrackerType type) => type switch
    {
        TrackerType.Jira => p.JiraTrigger,
        TrackerType.GitHub => p.GithubTrigger,
        TrackerType.GitLab => p.GitlabTrigger,
        TrackerType.AzureDevOps => p.AzuredevopsTrigger,
        _ => null,
    };

    private static void SetTrigger(RawProjectEntry p, TrackerType type, WebhookTriggerConfig wrapper)
    {
        switch (type)
        {
            case TrackerType.Jira: p.JiraTrigger = (JiraTriggerConfig)wrapper; break;
            case TrackerType.GitHub: p.GithubTrigger = wrapper; break;
            case TrackerType.GitLab: p.GitlabTrigger = wrapper; break;
            case TrackerType.AzureDevOps: p.AzuredevopsTrigger = wrapper; break;
        }
    }

    private static WebhookTriggerConfig CreateWrapper(TrackerType type) =>
        type == TrackerType.Jira ? new JiraTriggerConfig() : new WebhookTriggerConfig();

    private static ResolutionStrategy ParseStrategy(string projectName, string key) => key.ToLowerInvariant() switch
    {
        "tag" => ResolutionStrategy.Tag,
        "area_path" => ResolutionStrategy.AreaPath,
        "repo" => ResolutionStrategy.Repo,
        "to_address" => ResolutionStrategy.ToAddress,
        _ => throw new ConfigurationException(
            $"Project '{projectName}': resolution shorthand key '{key}' is not a known strategy " +
            "(tag/area_path/repo/to_address)."),
    };

    private static bool IsEmpty<TValue>(IReadOnlyDictionary<string, TValue>? map) => map is null || map.Count == 0;

    private static (string Key, string Value) First(Dictionary<string, string> map)
    {
        foreach (var kv in map) return (kv.Key, kv.Value);
        return (string.Empty, string.Empty);
    }
}
