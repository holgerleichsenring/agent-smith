using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// Raw YAML shape for the entire agentsmith.yml file. Loader binds YamlDotNet
/// to this, then builds the public <see cref="AgentSmithConfig"/> via the
/// catalog resolver.
/// </summary>
public sealed class RawAgentSmithConfig
{
    public Dictionary<string, AgentConfig> Agents { get; set; } = new();
    public Dictionary<string, RawConnectionEntry> Connections { get; set; } = new();
    public Dictionary<string, RawRepoEntry> Repos { get; set; } = new();
    public Dictionary<string, RawTrackerEntry> Trackers { get; set; } = new();
    public Dictionary<string, string> PipelineTriggers { get; set; } = new();
    public Dictionary<string, RawProjectEntry> Projects { get; set; } = new();
    // p0345: config-studio-owned MCP server catalog. Not consumed by the loader
    // pipeline yet (p0342 wires it in); bound here so a studio export round-trips.
    public Dictionary<string, RawMcpServerEntry> McpServers { get; set; } = new();
    public Dictionary<string, string> Secrets { get; set; } = new();
    public List<RawRegistryEntry> Registries { get; set; } = new();

    public QueueConfig Queue { get; set; } = new();
    public SkillsConfig Skills { get; set; } = new();
    public string? PrimaryProvider { get; set; }
    public LoopLimitsConfig Limits { get; set; } = new();
    public PipelineStorageConfig PipelineStorage { get; set; } = new();
    public PipelineDataFlowConfig PipelineDataFlow { get; set; } = new();
    public DeploymentConfig Deployment { get; set; } = new();
    public SandboxGlobalConfig Sandbox { get; set; } = new();
    public OrchestratorGlobalConfig Orchestrator { get; set; } = new();
    public DialogueGlobalConfig Dialogue { get; set; } = new(); // p0327
    public PersistenceConfig Persistence { get; set; } = new();

    /// <summary>
    /// p0503b: the authority a presented token is validated against. NULLABLE and without
    /// an initializer on purpose: an absent <c>auth:</c> key leaves it null, while a key
    /// whose contents the loader could not match yields an instance with no authority —
    /// and telling those two apart is what makes "present but unusable" reportable.
    /// It carries no taxonomy descriptor, so the config store never stores it and no
    /// export can emit one.
    /// </summary>
    public TokenAuthorityConfig? Auth { get; set; }

    /// <summary>
    /// 2026-08-25-1806: what a role name means here, and which claims it is read out of.
    /// A singleton config doc like every other one — stored, edited in the studio, and
    /// applied to the next request. Its bootstrap counterpart under <see cref="Auth"/> is
    /// only the seed an installation that has not migrated is imported from.
    /// </summary>
    public RoleMappingConfig RoleMapping { get; set; } = new();

    /// <summary>p0423: whether a run records its conversation, not only its numbers.</summary>
    public TraceConfig Trace { get; set; } = new();
    public PipelineCostCapConfig PipelineCostCap { get; set; } = new();
}
