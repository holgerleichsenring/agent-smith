namespace AgentSmith.Infrastructure.Models;

/// <summary>
/// Named constants for agent provider configuration defaults.
/// Replaces magic numbers scattered across provider implementations.
/// </summary>
public static class AgentDefaults
{
    /// <summary>Default max tokens for primary/planning model calls.</summary>
    public const int DefaultMaxTokens = 8192;

    /// <summary>Max tokens for context compaction summaries.</summary>
    public const int CompactionMaxTokens = 2048;

    /// <summary>Max file structure lines to include in prompts.</summary>
    public const int MaxFileStructureLines = 200;

    /// <summary>Max text content length before truncation in compaction.</summary>
    public const int MaxTextContentLength = 2000;

    /// <summary>Jitter factor (±25%) applied to retry delays.</summary>
    public const double RetryJitterFactor = 0.25;

    /// <summary>Default GitLab base URL when GITLAB_URL is not configured.</summary>
    public const string DefaultGitLabBaseUrl = "https://gitlab.com";
}
