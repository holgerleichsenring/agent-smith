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

    public ModelAssignment? ContextGeneration { get; set; }

    /// <summary>2026-09-25-2fa7: optional, like Reasoning and ContextGeneration. It used to carry a
    /// hard-coded Claude model, which was inert while nothing requested the role and would have
    /// handed an OpenAI or ollama agent another provider's model string the moment something did.
    /// Unset, it resolves to the agent's own Scout assignment — p0374's measured binding.</summary>
    public ModelAssignment? CodeMapGeneration { get; set; }
}
