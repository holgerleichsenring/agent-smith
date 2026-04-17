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

    /// <summary>
    /// Whitelist of states considered "open". Used by ListOpenAsync to filter work items.
    /// If empty, provider uses its built-in defaults.
    /// </summary>
    public List<string> OpenStates { get; set; } = [];

    /// <summary>
    /// Target state when closing a ticket (e.g. "Closed", "Done", "Resolved").
    /// If null, provider uses its built-in default.
    /// </summary>
    public string? DoneStatus { get; set; }

    /// <summary>
    /// Jira only: fallback transition name for CloseTicketAsync (e.g. "Close", "Resolve").
    /// If null, Jira provider falls back to "Close".
    /// </summary>
    public string? CloseTransitionName { get; set; }

    /// <summary>
    /// Additional fields to fetch from work items (e.g. custom Azure DevOps fields).
    /// Provider merges these with its standard field set.
    /// </summary>
    public List<string> ExtraFields { get; set; } = [];
}
