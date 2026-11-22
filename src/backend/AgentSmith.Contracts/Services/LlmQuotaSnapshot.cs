namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: one deployment's quota as this process sees it now — free tokens and requests of the
/// per-minute limit (never below zero; <paramref name="SecondsToFull"/> also covers a debt), how long a refusal
/// still pauses it, how many calls wait, and the provider's own last reading with when it was read.
/// </summary>
public sealed record LlmQuotaSnapshot(
    string Provider,
    string Deployment,
    int TokensPerMinute,
    int RequestsPerMinute,
    long FreeTokens,
    long FreeRequests,
    double SecondsToFull,
    double PausedSeconds,
    int Waiting,
    long? ProviderRemainingTokens,
    long? ProviderRemainingRequests,
    DateTimeOffset? ProviderReadAt);
