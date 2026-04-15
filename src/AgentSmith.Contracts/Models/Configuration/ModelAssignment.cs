namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Maps a task type to a specific model and token budget.
/// ProviderType and Endpoint are optional — null means use the default cloud provider.
/// </summary>
public sealed class ModelAssignment
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 8192;
    public string? Deployment { get; set; }
    public string? ProviderType { get; set; }
    public string? Endpoint { get; set; }
}
