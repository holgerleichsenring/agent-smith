namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for a named pipeline (ordered list of command names).
/// </summary>
public sealed class PipelineConfig
{
    public List<string> Commands { get; set; } = new();
}
