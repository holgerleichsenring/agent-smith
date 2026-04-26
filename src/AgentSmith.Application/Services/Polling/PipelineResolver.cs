using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Resolves which pipeline a polled ticket should run, based on the project's
/// trigger config and the ticket's labels. Mirrors the per-platform webhook
/// ResolvePipeline... methods so polling and webhooks route consistently.
///
/// Rules:
///   - Lifecycle labels (prefix "agent-smith:") are filtered before matching;
///     they never satisfy a pipeline_from_label entry.
///   - Iteration order over pipeline_from_label is the dictionary's insertion
///     order. First key whose value appears in the (filtered) labels wins.
///     Operators with overlapping keys should keep entries unambiguous.
///   - If pipeline_from_label is empty, returns trigger.DefaultPipeline.
///   - If pipeline_from_label is non-empty but nothing matches, returns null
///     so the caller can apply its own fallback.
/// </summary>
public static class PipelineResolver
{
    private const string LifecycleLabelPrefix = "agent-smith:";

    public static string? Resolve(WebhookTriggerConfig trigger, IEnumerable<string> labels)
    {
        var userLabels = labels
            .Where(l => !string.IsNullOrEmpty(l)
                && !l.StartsWith(LifecycleLabelPrefix, StringComparison.Ordinal))
            .ToList();

        foreach (var (configLabel, pipeline) in trigger.PipelineFromLabel)
        {
            if (userLabels.Contains(configLabel, StringComparer.OrdinalIgnoreCase))
                return pipeline;
        }

        return trigger.PipelineFromLabel.Count == 0 ? trigger.DefaultPipeline : null;
    }
}
