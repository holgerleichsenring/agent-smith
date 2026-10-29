using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281b: builds the effective per-project trigger from the tracker's workflow fields
/// (trigger_statuses / done_status / failed_status / pipeline_from_label) plus the
/// project's flat <c>resolution:</c> shorthand. The tracker OWNS the workflow; a project
/// supplies only its resolution, and the trigger TYPE is inferred from the tracker type.
/// 2026-10-29-6bd3a: and nothing else — a per-project trigger block no longer overrides it
/// field by field; LegacyTriggerFold has moved every block onto project and tracker before this
/// runs, so what the studio shows is what is built here.
/// Runs on the raw config before catalog resolution, so every downstream consumer reads
/// the already-merged trigger and nothing changes past this point.
/// </summary>
public sealed class EffectiveTriggerBuilder
{
    public void Apply(string projectName, RawProjectEntry project, RawTrackerEntry? tracker)
    {
        if (tracker is null || IsEmpty(project.Resolution)) return;

        var wrapper = CreateWrapper(tracker.Type);
        FillResolution(projectName, wrapper, project.Resolution!);
        FillWorkflow(wrapper, tracker);
        SetTrigger(project, tracker.Type, wrapper);
    }

    private static void FillResolution(
        string projectName, WebhookTriggerConfig wrapper, Dictionary<string, string> shorthand)
    {
        var (key, value) = First(shorthand);
        wrapper.ProjectResolution = new ProjectResolutionConfig
        {
            Strategy = ParseStrategy(projectName, key),
            Value = value,
        };
    }

    private static void FillWorkflow(WebhookTriggerConfig wrapper, RawTrackerEntry tracker)
    {
        var statuses = TrackerStatuses(tracker);
        if (statuses.Count > 0) wrapper.TriggerStatuses = statuses;
        if (!string.IsNullOrEmpty(tracker.DoneStatus)) wrapper.DoneStatus = tracker.DoneStatus;
        wrapper.FailedStatus = tracker.FailedStatus;
        wrapper.NeedsClarificationStatus = tracker.NeedsClarificationStatus;
        wrapper.NotImplementableStatus = tracker.NotImplementableStatus; // p0390
        wrapper.InProgressStatus = tracker.InProgressStatus;
        // 2026-09-16-a4d7: the map and the answer for 'nothing matched' are one decision,
        // so the tracker supplies both.
        wrapper.DefaultPipeline = tracker.DefaultPipeline;
        if (tracker.PipelineFromLabel is { Count: > 0 } labels)
            wrapper.PipelineFromLabel = new Dictionary<string, string>(labels);
        wrapper.CommentKeyword = tracker.CommentKeyword;
        wrapper.PrTriggerLabel = tracker.PrTriggerLabel;
        if (wrapper is not JiraTriggerConfig jira) return;
        // Unset keeps the class's 'Agent Smith', which every project without a block always had.
        if (!string.IsNullOrWhiteSpace(tracker.AssigneeName)) jira.AssigneeName = tracker.AssigneeName;
        jira.Secret = tracker.WebhookSecret;
    }

    private static List<string> TrackerStatuses(RawTrackerEntry tracker) =>
        tracker.TriggerStatuses.Count > 0 ? tracker.TriggerStatuses
        : tracker.OpenStates.Count > 0 ? tracker.OpenStates
        : [];

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
