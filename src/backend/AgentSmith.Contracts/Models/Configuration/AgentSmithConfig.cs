namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Root configuration model. Built by the loader from raw YAML plus the
/// catalog resolver — never directly bound by YamlDotNet.
///
/// Projects expose <see cref="ResolvedProject"/>: catalog references
/// (agent/tracker/repos) are already materialized to records by the time
/// any consumer sees this object.
/// </summary>
public sealed class AgentSmithConfig
{
    public Dictionary<string, AgentConfig> Agents { get; init; } = new();
    public Dictionary<string, RepoConnection> Repos { get; init; } = new();
    public Dictionary<string, TrackerConnection> Trackers { get; init; } = new();
    public PipelineTriggerMap PipelineTriggers { get; init; } = PipelineTriggerMap.Empty;
    public Dictionary<string, ResolvedProject> Projects { get; init; } = new();
    public IReadOnlyDictionary<string, string> Secrets { get; init; } = new Dictionary<string, string>();

    public QueueConfig Queue { get; init; } = new();
    public SkillsConfig Skills { get; init; } = new();
    public string? PrimaryProvider { get; init; }
    public LoopLimitsConfig Limits { get; init; } = new();
    public PipelineCostCapConfig PipelineCostCap { get; init; } = new();
    public PipelineStorageConfig PipelineStorage { get; init; } = new();
    public PipelineDataFlowConfig PipelineDataFlow { get; init; } = new();
    public SandboxGlobalConfig Sandbox { get; init; } = new();
    public OrchestratorGlobalConfig Orchestrator { get; init; } = new();

    /// <summary>
    /// Empty placeholder. Used by DI default registration when no real config
    /// has been loaded yet (composition roots later replace it with the
    /// loader's output).
    /// </summary>
    public static AgentSmithConfig Empty() => new();
}
