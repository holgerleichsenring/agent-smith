namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Configuration for Anthropic prompt caching behavior.
/// </summary>
public class CacheConfig
{
    public bool Enabled { get; set; } = true;
    public string Strategy { get; set; } = "automatic";
}
