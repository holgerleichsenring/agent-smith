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
}
