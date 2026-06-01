namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for an AI agent provider (Claude, OpenAI, Gemini, Ollama).
/// </summary>
public sealed class AgentConfig
{
    public string Type { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? Deployment { get; set; }
    public string? ApiVersion { get; set; }
    public string? ApiKeySecret { get; set; }
    public RetryConfig Retry { get; set; } = new();
    public CacheConfig Cache { get; set; } = new();
    public CompactionConfig Compaction { get; set; } = new();
    public ModelRegistryConfig? Models { get; set; }
    public PricingConfig Pricing { get; set; } = new();
    public ParallelismConfig Parallelism { get; set; } = new();
    public RateLimitConfig? RateLimit { get; set; }
}

/// <summary>
/// p0188: per-agent rate-limit override. When unset, ChatClientFactory picks
/// a conservative default based on agent type (subscription tokens get a
/// tighter budget than API keys).
/// </summary>
public sealed class RateLimitConfig
{
    public int? RequestsPerMinute { get; set; }
    public int? InputTokensPerMinute { get; set; }
}
