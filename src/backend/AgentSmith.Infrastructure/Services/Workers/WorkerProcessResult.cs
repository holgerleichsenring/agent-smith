namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0416: what one worker invocation produced. <see cref="Duration"/> is recorded because
/// a per-call subprocess costs nothing in tokens and everything in wall time — a run that
/// takes four hours is a finding, not a footnote.
/// </summary>
public sealed record WorkerProcessResult(
    int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration, bool TimedOut);
