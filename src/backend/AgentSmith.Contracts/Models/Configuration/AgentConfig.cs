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

    /// <summary>
    /// p0235: per-request network timeout for a single LLM HTTP call, in
    /// seconds. The Azure/OpenAI SDK (System.ClientModel) defaults
    /// <c>NetworkTimeout</c> to 100s — a large gpt-4.1 completion carrying a
    /// big analyze-code context routinely exceeds that, and the SDK then throws
    /// a TaskCanceledException that surfaces as a bare "A task was canceled."
    /// Default 300s; still bounded in practice by
    /// <c>limits.max_seconds_per_skill_call</c> and
    /// <c>sandbox.step_timeout_seconds</c>.
    /// </summary>
    public int NetworkTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// p0258: how many times the coding master may RE-ATTEMPT after its own
    /// build/tests come back red before it gives up — surfaced to the master
    /// skill as the {MaxFixIterations} prompt variable. A run whose edit broke a
    /// test (or whose test now asserts the old behaviour) must investigate and
    /// fix, not stop at the first red — but bounded so a hopeless loop ends.
    /// Default 3, so no config is needed when 3 fits; raise via
    /// <c>agent.max_fix_iterations</c> for harder tickets.
    /// </summary>
    public int MaxFixIterations { get; set; } = 3;
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
