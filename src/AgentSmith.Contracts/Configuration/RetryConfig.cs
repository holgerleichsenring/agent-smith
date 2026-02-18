namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Configuration for retry behavior on transient API failures and rate limits.
/// </summary>
public class RetryConfig
{
    public int MaxRetries { get; set; } = 5;
    public int InitialDelayMs { get; set; } = 2000;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int MaxDelayMs { get; set; } = 60000;
}
