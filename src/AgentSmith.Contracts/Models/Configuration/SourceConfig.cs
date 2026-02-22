namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for a source code provider (GitHub, GitLab, AzureRepos, Local).
/// </summary>
public sealed class SourceConfig
{
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Path { get; set; }
    public string Auth { get; set; } = string.Empty;
}
