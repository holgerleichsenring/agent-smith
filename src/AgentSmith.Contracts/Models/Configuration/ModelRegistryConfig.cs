namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for task-specific model routing.
/// Each task type can be assigned to a different model for cost optimization.
/// </summary>
public sealed class ModelRegistryConfig
{
    public ModelAssignment Scout { get; set; } = new()
    {
        Model = "claude-haiku-4-5-20251001",
        MaxTokens = 4096
    };

    public ModelAssignment Primary { get; set; } = new()
    {
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 8192
    };

    public ModelAssignment Planning { get; set; } = new()
    {
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 4096
    };

    public ModelAssignment? Reasoning { get; set; }

    public ModelAssignment Summarization { get; set; } = new()
    {
        Model = "claude-haiku-4-5-20251001",
        MaxTokens = 2048
    };
}
