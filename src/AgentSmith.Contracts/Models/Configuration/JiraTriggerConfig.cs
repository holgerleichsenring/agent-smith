namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configures automatic pipeline triggering when a Jira issue is assigned to Agent Smith.
/// </summary>
public sealed class JiraTriggerConfig
{
    public string AssigneeName { get; set; } = "Agent Smith";
    public string? Secret { get; set; }
    public Dictionary<string, string> PipelineFromLabel { get; set; } = new();
    public string DefaultPipeline { get; set; } = "fix-bug";
    public List<string> TriggerStatuses { get; set; } = ["Open"];
    public string DoneStatus { get; set; } = "In Review";
    public string? CommentKeyword { get; set; }
}
