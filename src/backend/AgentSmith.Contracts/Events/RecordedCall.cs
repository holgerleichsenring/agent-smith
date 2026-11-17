namespace AgentSmith.Contracts.Events;

/// <summary>
/// 2026-11-17-7c9bc: one model call with exactly the figures its LlmCallFinished recorded — the model, the
/// execution stage and repository of its call scope, the uncached and cached input, output, cache creation,
/// the cost (a worker call's is its CLI's own figure) and whether a worker CLI answered it.
/// </summary>
public sealed record RecordedCall(
    string Model,
    string? Stage,
    string? RepoName,
    long TokensIn,
    long TokensOut,
    long CachedTokensIn,
    long CacheCreationTokensIn,
    decimal CostUsd,
    bool IsWorker);
