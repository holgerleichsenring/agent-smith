namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Per-project polling configuration. Disabled by default so webhook-only
/// deployments keep their existing behaviour.
/// </summary>
public sealed class PollingConfig
{
    public bool Enabled { get; set; } = false;
    public int IntervalSeconds { get; set; } = 60;
    public int JitterPercent { get; set; } = 10;
}
