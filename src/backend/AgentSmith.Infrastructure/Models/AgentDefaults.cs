namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Named constants for agent provider configuration defaults.
/// Replaces magic numbers scattered across provider implementations.
/// </summary>
public static class AgentDefaults
{
    /// <summary>Jitter factor (±25%) applied to retry delays.</summary>
    public const double RetryJitterFactor = 0.25;

    /// <summary>Default GitLab base URL when GITLAB_URL is not configured.</summary>
    public const string DefaultGitLabBaseUrl = "https://gitlab.com";
}
