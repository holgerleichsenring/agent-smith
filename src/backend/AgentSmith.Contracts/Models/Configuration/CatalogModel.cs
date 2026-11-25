namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// One entry of an agent's model catalog: a model the agent may call, declared once with
/// everything that makes a call to it — deployment, output cap, input window, and the
/// provider and endpoint when it answers somewhere other than the agent's own. A role names
/// the entry with <c>use:</c>, so two roles on one deployment are one entry, not two copies.
/// <see cref="Tier"/> is optional; an untiered entry is never reported.
/// </summary>
public sealed class CatalogModel
{
    public string Model { get; set; } = string.Empty;
    public string? Deployment { get; set; }
    public int MaxTokens { get; set; } = 8192;
    public int? ContextWindowTokens { get; set; }
    public string? ProviderType { get; set; }
    public string? Endpoint { get; set; }
    public ModelTier? Tier { get; set; }

    /// <summary>2026-11-25-9992k: the wire this model is called on — <see cref="ModelApi"/>; unset is Chat Completions.</summary>
    public string? Api { get; set; }

    /// <summary>2026-11-25-9992k: the reasoning effort sent as written (Responses only).</summary>
    public string? ReasoningEffort { get; set; }

    /// <summary>The assignment a role naming this entry answers with.</summary>
    public ModelAssignment ToAssignment() => new()
    {
        Model = Model,
        Deployment = Deployment,
        MaxTokens = MaxTokens,
        ContextWindowTokens = ContextWindowTokens,
        ProviderType = ProviderType,
        Endpoint = Endpoint,
        Api = Api,
        ReasoningEffort = ReasoningEffort,
    };
}
