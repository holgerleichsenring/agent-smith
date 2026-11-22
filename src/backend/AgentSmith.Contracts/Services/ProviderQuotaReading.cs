namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: the quota the provider says is left, as its response headers state it
/// (<c>x-ratelimit-remaining-tokens</c>, <c>x-ratelimit-remaining-requests</c>). Null where a header was absent.
/// </summary>
public sealed record ProviderQuotaReading(long? RemainingTokens, long? RemainingRequests);
