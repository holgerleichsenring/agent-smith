namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Project entry with catalog references already materialized to records.
/// Produced by ConfigCatalogResolver after the loader parses raw YAML.
///
/// Repos is the multi-repo source of truth. Consumers read the run's repos from
/// PipelineContext under ContextKeys.Repos (set by ExecutePipelineUseCase from
/// project.Repos, optionally filtered by ContextKeys.SourceOverrideRepo).
/// </summary>
public sealed record ResolvedProject
{
    public string Name { get; init; } = string.Empty;
    public AgentConfig Agent { get; init; } = new();
    public TrackerConnection Tracker { get; init; } = new();
    public IReadOnlyList<RepoConnection> Repos { get; init; } = [];

    public string Pipeline { get; init; } = string.Empty;
    public string? CodingPrinciplesPath { get; init; }
    public string SkillsPath { get; init; } = "skills/coding";
    public IReadOnlyList<PipelineDefinition> Pipelines { get; init; } = [];
    public string? DefaultPipeline { get; init; }
    public JiraTriggerConfig? JiraTrigger { get; init; }
    public WebhookTriggerConfig? GithubTrigger { get; init; }
    public WebhookTriggerConfig? GitlabTrigger { get; init; }
    public WebhookTriggerConfig? AzuredevopsTrigger { get; init; }
    public PollingConfig Polling { get; init; } = new();
    public SandboxConfig? Sandbox { get; init; }
    public OrchestratorConfig? Orchestrator { get; init; }
}
