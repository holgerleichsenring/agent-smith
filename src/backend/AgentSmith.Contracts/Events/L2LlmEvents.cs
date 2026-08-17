using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Events;

// p0423: PromptChars is an additive trailing optional. The event carried the prompt's
// HASH and not its SIZE, which is the one number that would have named "Prompt is too
// long" the moment it started growing; identity without measure cost two runs.
public sealed record LlmCallStartedEvent(
    string RunId,
    string Model,
    string Role,
    string PromptHash,
    DateTimeOffset Timestamp,
    string? Phase = null,
    string? RepoName = null,
    long PromptChars = 0)
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
    long CacheCreationTokensIn = 0,
    // p0363: how much of DurationMs was the client-side rate-limiter waiting
    // for TPM/RPM budget — the split that answers "was that hour real work or
    // waiting?". 0 for calls that passed the bucket without queueing (and for
    // events from pre-p0363 servers).
    long ThrottleWaitMs = 0,
    // p0423: the sizes beside the token counts. Tokens are what the provider billed;
    // characters are what the framework built, and the two diverge exactly where a
    // defect lives. Outcome makes a call that DIED visible at all — before p0423 a
    // provider error emitted no finished event, so a run that failed on its last call
    // recorded the call as never having ended.
    long PromptChars = 0,
    long ResponseChars = 0,
    WorkOutcome Outcome = WorkOutcome.Ok,
    int Attempt = 1)
    : RunEvent(RunId, EventType.LlmCallFinished, Timestamp), IMeasuredWork
{
    [JsonIgnore]
    public WorkMeasure Measure =>
        new(DurationMs, PromptChars, ResponseChars, ResponseChars, Outcome, Attempt);
}
