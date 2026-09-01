namespace AgentSmith.Contracts.Models.Configuration;

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
