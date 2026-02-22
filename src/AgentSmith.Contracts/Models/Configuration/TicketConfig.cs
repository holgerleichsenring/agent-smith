namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for a ticket provider (AzureDevOps, Jira, GitHub).
/// </summary>
public sealed class TicketConfig
{
    public string Type { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string? Project { get; set; }
    public string? Url { get; set; }
    public string Auth { get; set; } = string.Empty;
}
