namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Configuration for a named pipeline (ordered list of command names).
/// </summary>
public class PipelineConfig
{
    public List<string> Commands { get; set; } = new();
}
