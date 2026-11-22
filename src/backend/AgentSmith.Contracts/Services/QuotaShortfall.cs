namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-ab45: a call that needs more than its deployment's whole minute — <paramref name="Needed"/> tokens, of
/// them <paramref name="Input"/> input and <paramref name="Output"/> the max_tokens the provider counts, against a
/// per-minute budget of <paramref name="Budget"/>.
/// </summary>
public sealed record QuotaShortfall(long Needed, long Input, long Output, int Budget);
