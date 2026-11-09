namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: ONE model call, in call order — the pair that names a wall before it becomes
/// a stalled call. A run whose prompt grew 151k -> 357k while its answers shrank
/// 3,886 -> 0 bytes is unreadable as a table and obvious as a plot, so the series is
/// served as points and drawn, never summed.
/// </summary>
/// <param name="Index">Position in the run's call order, 1-based.</param>
/// <param name="SpecId">The spliced spec the call belongs to; null outside any spec.</param>
/// <param name="PromptId">2026-11-09-0620a: the stored prompt of the call; null for calls before it or unstored.</param>
public sealed record RunCallPoint(
    int Index,
    string? SpecId,
    int? StepIndex,
    string? Role,
    string? Model,
    long PromptChars,
    long AnswerChars,
    long DurationMs,
    long ThrottleWaitMs,
    string Outcome,
    int Attempt,
    string? PromptId = null,
    int ToolCount = 0,
    long TokensIn = 0,
    long CachedTokensIn = 0);
