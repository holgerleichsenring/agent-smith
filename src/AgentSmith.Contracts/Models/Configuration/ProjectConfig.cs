namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for a single project. Set <see cref="Pipelines"/> +
/// <see cref="DefaultPipeline"/> for new configs; the legacy <see cref="Pipeline"/>
/// + <see cref="SkillsPath"/> single-string fields are retained for backward
/// compatibility and are translated into a synthetic single-element
/// <see cref="Pipelines"/> by the configuration loader when <see cref="Pipelines"/>
/// is empty.
/// </summary>
public sealed class ProjectConfig
{
    public SourceConfig Source { get; set; } = new();
    public TicketConfig Tickets { get; set; } = new();
    public AgentConfig Agent { get; set; } = new();
    public string Pipeline { get; set; } = string.Empty;
    public string? CodingPrinciplesPath { get; set; }
    public string SkillsPath { get; set; } = "skills/coding";
    public List<PipelineDefinition> Pipelines { get; set; } = [];
    public string? DefaultPipeline { get; set; }
    public JiraTriggerConfig? JiraTrigger { get; set; }
    public WebhookTriggerConfig? GithubTrigger { get; set; }
    public WebhookTriggerConfig? GitlabTrigger { get; set; }
    public WebhookTriggerConfig? AzuredevopsTrigger { get; set; }
    public PollingConfig Polling { get; set; } = new();
    public SandboxConfig? Sandbox { get; set; }
}
