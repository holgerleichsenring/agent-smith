namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: what one worker invocation produced. <see cref="Duration"/> is recorded because
/// a per-call subprocess costs nothing in tokens and everything in wall time — a run that
/// takes four hours is a finding, not a footnote.
/// <para>
/// 2026-09-01-b0d7: when the CLI answered with its structured result, the answer is
/// unwrapped HERE, at the process boundary, and everything downstream — the
/// retry-on-silence check, the process guard, the empty-turn path, the reply parser —
/// decides on <see cref="AnswerText"/>. It has to be here: a structured result is never
/// empty, so a check on raw stdout would stop seeing silence, and an empty answer would
/// become a thrown call where it used to be a nudge that saved the run.
/// </para>
/// </summary>
public sealed record WorkerProcessResult(
    int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration, bool TimedOut)
{
    // Read once, at construction, so every reader of this result sees the same verdict.
    private readonly WorkerCliEnvelope? _envelope =
        WorkerCliEnvelope.TryRead(StandardOutput, out var read) ? read : null;

    /// <summary>The CLI's structured result, or null when it simply printed an answer.</summary>
    public WorkerCliEnvelope? Envelope => _envelope;

    /// <summary>What the worker actually answered, unwrapped when there is an envelope.</summary>
    public string AnswerText => _envelope?.AnswerText ?? StandardOutput;
}
