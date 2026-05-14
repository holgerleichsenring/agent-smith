namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Root configuration model deserialized from agentsmith.yml.
/// </summary>
public sealed class AgentSmithConfig
{
    public Dictionary<string, ProjectConfig> Projects { get; set; } = new();
    public Dictionary<string, string> Secrets { get; set; } = new();

    /// <summary>
    /// Process-wide queue settings. Shared across all projects — one queue, one consumer
    /// with bounded concurrency as the backpressure knob.
    /// </summary>
    public QueueConfig Queue { get; set; } = new();

    /// <summary>
    /// Skill catalog source. The server pulls or mounts the catalog at boot
    /// based on this section. See <see cref="SkillsConfig"/>.
    /// </summary>
    public SkillsConfig Skills { get; set; } = new();

    /// <summary>
    /// Active provider for skill resolution (claude, openai, azure-openai, gemini, ollama).
    /// When set, SKILL.&lt;provider&gt;.md files in skill directories are picked up as
    /// overrides over the base SKILL.md. Null/empty disables provider overrides
    /// (default — base SKILL.md always wins).
    /// </summary>
    public string? PrimaryProvider { get; set; }

    /// <summary>
    /// Hard limits for the per-skill agentic loop (token caps, wall-clock cap,
    /// tool-call caps, concurrency cap). Defaults match Phase B of the runtime design;
    /// see <see cref="LoopLimitsConfig"/>.
    /// </summary>
    public LoopLimitsConfig Limits { get; set; } = new();

    /// <summary>
    /// In-flight pipeline-storage settings (Redis TTL for transient artifacts).
    /// See <see cref="PipelineStorageConfig"/>.
    /// </summary>
    public PipelineStorageConfig PipelineStorage { get; set; } = new();

    /// <summary>
    /// p0128c data-flow gating settings. <c>Enforce=false</c> by default — the
    /// gate logs warnings on undeclared reads but doesn't fail the run.
    /// </summary>
    public PipelineDataFlowConfig PipelineDataFlow { get; set; } = new();

    /// <summary>
    /// Process-wide sandbox defaults — agent carrier registry + version that
    /// every spawned sandbox pod references unless a project's
    /// <see cref="SandboxConfig"/> block overrides them. See <see cref="SandboxGlobalConfig"/>.
    /// </summary>
    public SandboxGlobalConfig Sandbox { get; set; } = new();
}
