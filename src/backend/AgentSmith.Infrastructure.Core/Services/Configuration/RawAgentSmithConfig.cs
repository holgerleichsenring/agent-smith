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
    public Dictionary<string, RawRepoEntry> Repos { get; set; } = new();
    public Dictionary<string, RawTrackerEntry> Trackers { get; set; } = new();
    public Dictionary<string, string> PipelineTriggers { get; set; } = new();
    public Dictionary<string, RawProjectEntry> Projects { get; set; } = new();
    public Dictionary<string, string> Secrets { get; set; } = new();

    public QueueConfig Queue { get; set; } = new();
    public SkillsConfig Skills { get; set; } = new();
    public string? PrimaryProvider { get; set; }
    public LoopLimitsConfig Limits { get; set; } = new();
    public PipelineStorageConfig PipelineStorage { get; set; } = new();
    public PipelineDataFlowConfig PipelineDataFlow { get; set; } = new();
    public SandboxGlobalConfig Sandbox { get; set; } = new();
    public OrchestratorGlobalConfig Orchestrator { get; set; } = new();
    public PipelineCostCapConfig PipelineCostCap { get; set; } = new();
}
