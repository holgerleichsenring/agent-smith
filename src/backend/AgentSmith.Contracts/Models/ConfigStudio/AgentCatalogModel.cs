using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// One model of an agent's catalog as the studio edits it: the model id, its optional Azure
/// deployment, output cap (null = the default 8192), input window, provider and endpoint when
/// it answers somewhere other than the agent's own, and the operator's tier.
/// </summary>
public sealed record AgentCatalogModel(
    string Model,
    string? Deployment = null,
    int? MaxTokens = null,
    int? ContextWindowTokens = null,
    string? ProviderType = null,
    string? Endpoint = null,
    ModelTier? Tier = null);
