namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0369: one file and how often it was read/written in a run — the body of the
/// run-metrics "top files" lists.
/// </summary>
public sealed record FileAccessView(string Path, int Count);

/// <summary>
/// p0369: the per-run METRICS summary served on the run detail — WHERE a run's
/// time and cost went, so the operator can optimize without reconstructing it by
/// hand from raw events. A projection of <see cref="RunMetrics"/> (the fold drops
/// its internal redundancy state and keeps only the top-N file lists).
/// </summary>
public sealed record RunMetricsView(
    // Time split: real LLM work, the slice of it spent waiting on the token rate
    // limiter, and sandbox-command wall time.
    long LlmActiveMs,
    long ThrottleWaitMs,
    long SandboxCommandMs,
    // Tool usage.
    int Reads,
    int Writes,
    int RunCommands,
    int Greps,
    IReadOnlyList<FileAccessView> TopReadFiles,
    IReadOnlyList<FileAccessView> TopWriteFiles,
    // Redundancy: re-reads/re-writes of content whose hash matched the last seen
    // for that path — the waste signal (a re-read of CHANGED content is not here).
    int RedundantReads,
    int RedundantWrites,
    // Cache health: "cold" calls (>1024 input tokens, nothing served from cache),
    // warm->cold discards, and the total tokens re-processed uncached.
    int ColdLlmCalls,
    int CacheDiscards,
    long UncachedReprocessedTokens,
    // Build/test invocations and how many exited non-zero.
    int BuildTestInvocations,
    int BuildTestFailures,
    // 2026-11-25-9992k: the questions the run asked the operator (ask_human calls).
    int QuestionsAsked = 0);
