namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Configuration for an AI agent provider (Claude, OpenAI).
/// </summary>
public class AgentConfig
{
    public string Type { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public RetryConfig Retry { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();
}
