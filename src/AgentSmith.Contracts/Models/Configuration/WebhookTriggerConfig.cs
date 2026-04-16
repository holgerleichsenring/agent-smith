namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Base trigger configuration shared by all webhook platforms.
/// Provides status gating, pipeline-from-label mapping, done_status transition, and comment re-trigger.
/// </summary>
public class WebhookTriggerConfig
{
    public Dictionary<string, string> PipelineFromLabel { get; set; } = new();
    public string DefaultPipeline { get; set; } = "fix-bug";
    public List<string> TriggerStatuses { get; set; } = [];
    public string DoneStatus { get; set; } = "In Review";
    public string? CommentKeyword { get; set; }
}
