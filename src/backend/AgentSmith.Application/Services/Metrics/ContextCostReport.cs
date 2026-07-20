namespace AgentSmith.Application.Services.Metrics;

/// <summary>
/// p0356: the per-run context-cost telemetry — total tokens, the share served
/// from prompt cache, and tokens per DONE ledger item. Flat tokens-per-item is
/// the overlay's health signal; an upward trend means the conventions digest
/// (decide-once) is missing something. TokensPerDoneItem is null when no ledger
/// item is done — never a fake zero.
/// </summary>
public sealed record ContextCostReport(
    long TotalTokens,
    double CachedShare,
    int DoneItems,
    long? TokensPerDoneItem);
