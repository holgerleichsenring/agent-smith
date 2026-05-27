namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Base trigger configuration shared by all webhook platforms.
/// Provides status gating, pipeline-from-label mapping, done_status transition, and comment re-trigger.
/// ProjectResolution is required at the schema level (p0140a); the property is nullable here so
/// the validator can produce a typed error rather than the loader failing with a null-deref.
/// </summary>
public class WebhookTriggerConfig
{
    public ProjectResolutionConfig? ProjectResolution { get; set; }
    public Dictionary<string, string>? PipelineFromLabel { get; set; }
    public string DefaultPipeline { get; set; } = "fix-bug";
    public List<string> TriggerStatuses { get; set; } = [];
    public string DoneStatus { get; set; } = "In Review";
    public string? CommentKeyword { get; set; }
}
