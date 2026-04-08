namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for automatic security fix generation.
/// </summary>
public sealed class AutoFixConfig
{
    public bool Enabled { get; set; }
    public string SeverityThreshold { get; set; } = "High";
    public bool ConfirmBeforeFix { get; set; } = true;
    public int MaxConcurrent { get; set; } = 3;
    public List<string> ExcludedPatterns { get; set; } = [];
}
