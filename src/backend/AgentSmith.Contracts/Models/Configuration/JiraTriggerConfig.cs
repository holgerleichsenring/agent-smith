namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configures automatic pipeline triggering when a Jira issue is assigned to Agent Smith.
/// Inherits shared trigger config (status gate, pipeline_from_label, done_status, comment keyword).
/// </summary>
public sealed class JiraTriggerConfig : WebhookTriggerConfig
{
    public string AssigneeName { get; set; } = "Agent Smith";
    public string? Secret { get; set; }

    public JiraTriggerConfig()
    {
        TriggerStatuses = ["Open"];
    }
}
