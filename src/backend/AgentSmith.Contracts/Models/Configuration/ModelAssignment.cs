namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Maps a task type to a specific model and token budget.
/// ProviderType and Endpoint are optional: null ProviderType answers on the agent's own
/// provider, and null Endpoint takes the agent's endpoint when the role runs on that provider
/// (<see cref="EffectiveEndpoint"/>).
/// </summary>
public sealed class ModelAssignment
{
    /// <summary>
    /// The name of an entry in the agent's <c>catalog:</c> this role answers with. When set it
    /// wins: every inline field beside it is ignored by <see cref="ModelRoleChain"/>.
    /// </summary>
    public string? Use { get; set; }

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

    /// <summary>
    /// Where this role sends its requests: its own endpoint, else the agent's when the role
    /// runs on the agent's provider. <c>agent.endpoint</c> belongs to the agent's type (an
    /// Ollama host, an Azure resource, an OpenAI-compatible server), so a role that switches
    /// provider never inherits it.
    /// </summary>
    public string? EffectiveEndpoint(AgentConfig agent) =>
        !string.IsNullOrWhiteSpace(Endpoint) ? Endpoint
        : string.Equals(ProviderType ?? agent.Type, agent.Type, StringComparison.OrdinalIgnoreCase)
            ? NullIfBlank(agent.Endpoint)
            : null;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
