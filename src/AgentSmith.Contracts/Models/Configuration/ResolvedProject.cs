namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Project entry with catalog references already materialized to records.
/// Produced by ConfigCatalogResolver after the loader parses raw YAML.
///
/// Repos is the source of truth; Repo is a transitional single-repo accessor
/// (asserts Repos.Count == 1) introduced in p0139 to keep call-site migration
/// minimal — removed in p0140 when consumers iterate Repos directly.
/// </summary>
public sealed record ResolvedProject
{
    public string Name { get; init; } = string.Empty;
    public AgentConfig Agent { get; init; } = new();
    public TrackerConnection Tracker { get; init; } = new();
    public IReadOnlyList<RepoConnection> Repos { get; init; } = [];

    public RepoConnection Repo
    {
        get => Repos.Count == 1
            ? Repos[0]
            : throw new InvalidOperationException(
                $"Project '{Name}' has {Repos.Count} repos; use Repos directly. " +
                "Single-repo accessor is transitional (p0139→p0140).");
        init => Repos = new[] { value };
    }

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
