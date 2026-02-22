namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for Anthropic prompt caching behavior.
/// </summary>
public sealed class CacheConfig
{
    public bool IsEnabled { get; set; } = true;
    public string Strategy { get; set; } = "automatic";
}
