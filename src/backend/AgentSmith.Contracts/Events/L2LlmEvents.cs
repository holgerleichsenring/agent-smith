namespace AgentSmith.Contracts.Events;

public sealed record LlmCallStartedEvent(
    string RunId,
    string Model,
    string Role,
    string PromptHash,
    DateTimeOffset Timestamp,
    string? Phase = null,
    string? RepoName = null)
    : RunEvent(RunId, EventType.LlmCallStarted, Timestamp);

// p0323: CachedTokensIn / CacheCreationTokensIn are additive trailing optionals —
// events persisted before p0323 deserialise with 0, mirroring how Phase / RepoName
// were added in p0176a. CachedTokensIn = prompt tokens served from cache
// (Anthropic cache_read + OpenAI cached_tokens); CacheCreationTokensIn = tokens
// written to the cache this call (Anthropic only). For Anthropic, TokensIn is the
// uncached remainder — total prompt = TokensIn + CachedTokensIn + CacheCreationTokensIn.
public sealed record LlmCallFinishedEvent(
    string RunId,
    string Model,
    string Role,
    long TokensIn,
    long TokensOut,
    decimal CostUsd,
    long DurationMs,
    DateTimeOffset Timestamp,
    string? Phase = null,
    string? RepoName = null,
    long CachedTokensIn = 0,
    long CacheCreationTokensIn = 0)
    : RunEvent(RunId, EventType.LlmCallFinished, Timestamp);
