using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-29-6bd3a: one trigger block's fields onto one tracker, each only where the tracker has none —
/// the half of <see cref="LegacyTriggerFold"/> that knows the fields.
/// </summary>
internal sealed class LegacyTriggerTrackerFill(
    string project, string kind, RawTrackerEntry tracker,
    Dictionary<(RawTrackerEntry, string), (string Project, string Value)> filledBy,
    List<StartupFinding> findings)
{
    public void Apply(LegacyTriggerBlock block)
    {
        if (block.TriggerStatuses is { Count: > 0 } statuses
            && tracker.TriggerStatuses.Count == 0 && tracker.OpenStates.Count == 0)
            tracker.TriggerStatuses = [.. statuses];
        if (block.PipelineFromLabel is { Count: > 0 } labels && tracker.PipelineFromLabel is not { Count: > 0 })
            tracker.PipelineFromLabel = new Dictionary<string, string>(labels);
        tracker.DoneStatus = Terminal("done_status", tracker.DoneStatus, block.DoneStatus);
        tracker.FailedStatus = Terminal("failed_status", tracker.FailedStatus, block.FailedStatus);
        tracker.NeedsClarificationStatus = Terminal(
            "needs_clarification_status", tracker.NeedsClarificationStatus, block.NeedsClarificationStatus);
        tracker.NotImplementableStatus = Terminal(
            "not_implementable_status", tracker.NotImplementableStatus, block.NotImplementableStatus);
        tracker.InProgressStatus = Terminal("in_progress_status", tracker.InProgressStatus, block.InProgressStatus);
        tracker.DefaultPipeline = Field("default_pipeline", tracker.DefaultPipeline, block.DefaultPipeline);
        tracker.CommentKeyword = Field("comment_keyword", tracker.CommentKeyword, block.CommentKeyword);
        tracker.PrTriggerLabel = Field("pr_trigger_label", tracker.PrTriggerLabel, block.PrTriggerLabel);
        if (block is not LegacyJiraTriggerBlock jira) return;
        tracker.AssigneeName = Field("assignee_name", tracker.AssigneeName, jira.AssigneeName);
        tracker.WebhookSecret = Field("webhook_secret", tracker.WebhookSecret, jira.Secret);
    }

    private string? Terminal(string field, string? current, string? value)
    {
        if (current is null && value is not null && TriggerStatuses().Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            findings.Add(Finding(field,
                $"its {field} '{value}' is one of the tracker's trigger statuses, so it is not taken — on "
                + "the tracker it would re-claim every ticket it moves there"));
            return current;
        }
        return Field(field, current, value);
    }

    private string? Field(string field, string? current, string? value)
    {
        if (value is null) return current;
        if (current is null)
        {
            filledBy[(tracker, field)] = (project, value);
            return value;
        }
        if (filledBy.TryGetValue((tracker, field), out var first) && !string.Equals(first.Value, value, StringComparison.Ordinal))
            findings.Add(Finding(field,
                $"its {field} differs from the one project '{first.Project}' gave the same tracker, "
                + $"which keeps '{(field == "webhook_secret" ? "(secret)" : first.Value)}'"));
        return current;
    }

    private IReadOnlyList<string> TriggerStatuses() =>
        tracker.TriggerStatuses.Count > 0 ? tracker.TriggerStatuses : tracker.OpenStates;

    private StartupFinding Finding(string field, string why) => new(
        StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
        $"Project '{project}': the {kind} block was moved onto its tracker, but {why}.",
        project, kind, $"projects.{project}.{kind}.{field}");
}
