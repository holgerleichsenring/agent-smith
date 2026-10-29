using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-29-6bd3a: a per-project <c>jira_trigger</c> / <c>github_trigger</c> / <c>gitlab_trigger</c> /
/// <c>azuredevops_trigger</c> block as it was written, read only so <see cref="LegacyTriggerFold"/> can
/// move it into the project's resolution and its tracker. Every field is nullable: the trigger classes
/// initialise TriggerStatuses, DoneStatus and AssigneeName, so a block read through them could not say
/// which values it declared.
/// </summary>
public class LegacyTriggerBlock
{
    public ProjectResolutionConfig? ProjectResolution { get; set; }
    public Dictionary<string, string>? PipelineFromLabel { get; set; }
    public string? DefaultPipeline { get; set; }
    public List<string>? TriggerStatuses { get; set; }
    public string? DoneStatus { get; set; }
    public string? FailedStatus { get; set; }
    public string? NeedsClarificationStatus { get; set; }
    public string? NotImplementableStatus { get; set; }
    public string? InProgressStatus { get; set; }
    public string? CommentKeyword { get; set; }
    public string? PrTriggerLabel { get; set; }
}

/// <summary>A <c>jira_trigger</c> block: the shared fields plus the assignee and the webhook secret.</summary>
public sealed class LegacyJiraTriggerBlock : LegacyTriggerBlock
{
    public string? AssigneeName { get; set; }

    /// <summary>The webhook secret reference.</summary>
    public string? Secret { get; set; }
}
