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

    /// <summary>
    /// 2026-09-13-5fa0: what each context of this project is built after, resolved to the
    /// target's RepoConnection so a consumer need not re-read the catalog. Empty for every
    /// project that declares none, which is every project today.
    /// </summary>
    public IReadOnlyList<ProjectTemplate> Templates { get; init; } = [];

    /// <summary>
    /// 2026-10-01-7f7aa: the design sources this project may read, each carrying its secret's
    /// NAME — the token is looked up when a design is read, never stored on the project.
    /// </summary>
    public IReadOnlyList<DesignSource> DesignSources { get; init; } = [];

    public string Pipeline { get; init; } = string.Empty;
    public string? CodingPrinciplesPath { get; init; }
    public string SkillsPath { get; init; } = "skills";
    public IReadOnlyList<PipelineDefinition> Pipelines { get; init; } = [];
    public string? DefaultPipeline { get; init; }
    public JiraTriggerConfig? JiraTrigger { get; init; }
    public WebhookTriggerConfig? GithubTrigger { get; init; }
    public WebhookTriggerConfig? GitlabTrigger { get; init; }
    public WebhookTriggerConfig? AzuredevopsTrigger { get; init; }
    public PollingConfig Polling { get; init; } = new();
    public SandboxConfig? Sandbox { get; init; }
}
