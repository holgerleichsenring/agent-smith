namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Root configuration model deserialized from agentsmith.yml.
/// </summary>
public sealed class AgentSmithConfig
{
    public Dictionary<string, ProjectConfig> Projects { get; set; } = new();
    public Dictionary<string, PipelineConfig> Pipelines { get; set; } = new();
    public Dictionary<string, string> Secrets { get; set; } = new();
}
