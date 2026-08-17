namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: ONE model call, in call order — the pair that names a wall before it becomes
/// a stalled call. A run whose prompt grew 151k -> 357k while its answers shrank
/// 3,886 -> 0 bytes is unreadable as a table and obvious as a plot, so the series is
/// served as points and drawn, never summed.
/// </summary>
/// <param name="Index">Position in the run's call order, 1-based.</param>
/// <param name="PhaseId">The spliced phase the call belongs to; null outside any phase.</param>
public sealed record RunCallPoint(
    int Index,
    string? PhaseId,
    int? StepIndex,
    string? Role,
    string? Model,
    long PromptChars,
    long AnswerChars,
    long DurationMs,
    long ThrottleWaitMs,
    string Outcome,
    int Attempt);
