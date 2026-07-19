namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0345c: editable studio view of one agent catalog entry — the FULL raw
/// surface the loader already deserializes (endpoint / api version / timeout,
/// per-role model routing, pricing table, cache, compaction, retry), not just
/// the model names. <see cref="Models"/> maps role → assignment; the reserved
/// role <c>coding</c> is the agent's top-level <c>model:</c>/<c>deployment:</c>
/// pair, every other role patches the <c>models:</c> registry. Sections the raw
/// config carries but the entity leaves null are preserved on upsert (patch
/// semantics), so an export still round-trips through the real loader.
/// <see cref="KeySecret"/> carries the env-NAME of the provider key — never a value.
/// </summary>
public sealed record AgentEntity(
    string Id,
    string Provider,
    string? KeySecret,
    string? Endpoint,
    string? ApiVersion,
    int? NetworkTimeoutSeconds,
    IReadOnlyDictionary<string, AgentModelAssignment> Models,
    AgentPricing? Pricing,
    AgentCacheSettings? Cache,
    AgentCompactionSettings? Compaction,
    AgentRetrySettings? Retry)
{
    public AgentEntity() : this(
        string.Empty, string.Empty, null, null, null, null,
        new Dictionary<string, AgentModelAssignment>(), null, null, null, null)
    {
    }
}

/// <summary>One role's model routing: model id, optional Azure deployment, optional token budget.</summary>
public sealed record AgentModelAssignment(string Model, string? Deployment = null, int? MaxTokens = null);

/// <summary>The agent's pricing table: model name → USD per million tokens.</summary>
public sealed record AgentPricing(IReadOnlyDictionary<string, AgentModelPricing> Models)
{
    public AgentPricing() : this(new Dictionary<string, AgentModelPricing>()) { }
}

/// <summary>Per-model pricing in USD per million tokens.</summary>
public sealed record AgentModelPricing(
    decimal InputPerMillion, decimal OutputPerMillion, decimal? CacheReadPerMillion = null);

/// <summary>Prompt-cache settings (mirrors <c>cache:</c> on the raw agent).</summary>
public sealed record AgentCacheSettings(bool IsEnabled, string Strategy);

/// <summary>
/// Context-compaction settings (the studio-editable subset of <c>compaction:</c>;
/// the token-ratio trigger and deployment override are preserved untouched by upsert).
/// </summary>
public sealed record AgentCompactionSettings(
    bool IsEnabled,
    int ThresholdIterations,
    int MaxContextTokens,
    int KeepRecentIterations,
    string SummaryModel);

/// <summary>Transient-failure retry settings (mirrors <c>retry:</c> on the raw agent).</summary>
public sealed record AgentRetrySettings(
    int MaxRetries, int InitialDelayMs, double BackoffMultiplier, int MaxDelayMs);
