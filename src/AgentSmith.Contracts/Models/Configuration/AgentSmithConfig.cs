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
}
