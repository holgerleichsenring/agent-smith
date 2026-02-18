namespace AgentSmith.Contracts.Configuration;

/// <summary>
/// Maps a task type to a specific model and token budget.
/// </summary>
public class ModelAssignment
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 8192;
}
