using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for one entry inside the top-level `projects:` map.
/// Catalog references are still strings here; the resolver materializes them
/// into a <see cref="ResolvedProject"/>.
/// </summary>
public sealed class RawProjectEntry
{
    public string Agent { get; set; } = string.Empty;
    public string Tracker { get; set; } = string.Empty;
    public List<string> Repos { get; set; } = [];

    public string Pipeline { get; set; } = string.Empty;
    public List<RawPipelineEntry> Pipelines { get; set; } = [];
    public string? DefaultPipeline { get; set; }

    public string? CodingPrinciplesPath { get; set; }
    public string SkillsPath { get; set; } = "skills/coding";

    public JiraTriggerConfig? JiraTrigger { get; set; }
    public WebhookTriggerConfig? GithubTrigger { get; set; }
    public WebhookTriggerConfig? GitlabTrigger { get; set; }
    public WebhookTriggerConfig? AzuredevopsTrigger { get; set; }

    public PollingConfig Polling { get; set; } = new();
    public SandboxConfig? Sandbox { get; set; }
    public OrchestratorConfig? Orchestrator { get; set; }
}
