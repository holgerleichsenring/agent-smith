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

    /// <summary>
    /// 2026-08-27-3eb1: the INPUT window the deployment behind this role accepts, in
    /// tokens. <see cref="MaxTokens"/> is the OUTPUT cap and says nothing about it, so a
    /// compaction threshold of 200000 could sit beside a deployment that refuses at
    /// 128000 and nothing could notice. Null (the default) means unstated: nothing is
    /// derived from it and the chain behaves as it did. It is a property of the
    /// DEPLOYMENT, not of the model name — a role reading gpt-4.1-mini against a
    /// 4o-mini deployment answers in the deployment's window.
    /// </summary>
    public int? ContextWindowTokens { get; set; }
}
