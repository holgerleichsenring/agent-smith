namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-22-66e4b: the one rule for <c>agent.rate_limit</c>. Each number may be left unset (the provider default
/// applies to it); a number below 1 admits no call at all, so every call to the deployment would wait forever.
/// </summary>
public sealed partial class AgentConfig
{
    /// <summary>The config key the budget is set under, named by every message about it.</summary>
    public const string RateLimitKey = "agent.rate_limit";

    /// <summary>Why these numbers cannot be the budget (of agent <paramref name="agentName"/> when named), or null when
    /// they can.</summary>
    public static string? RateLimitProblem(int? requestsPerMinute, int? inputTokensPerMinute, string? agentName = null)
    {
        if (requestsPerMinute is not < 1 && inputTokensPerMinute is not < 1) return null;
        return (agentName is null ? "" : $"Agent '{agentName}': ")
            + $"rate_limit is {requestsPerMinute?.ToString() ?? "unset"} requests and {inputTokensPerMinute?.ToString() ?? "unset"} "
            + $"input tokens per minute; each must be at least 1, because below 1 no call to the model is ever admitted "
            + $"({RateLimitKey}.requests_per_minute, {RateLimitKey}.input_tokens_per_minute; leave one unset for the provider default).";
    }
}
