namespace AgentSmith.Contracts.Runs;

/// <summary>
/// p0423b: ONE command a phase ran, with the exit code it ended on. A phase that "failed
/// verification" is a sentence; the command that returned 1 and the size of the output it
/// produced is the evidence, and the story view shows the evidence.
/// </summary>
public sealed record RunCommandPoint(
    int Index,
    string? PhaseId,
    int? StepIndex,
    string Repo,
    string Command,
    int ExitCode,
    long DurationMs,
    long OutputChars,
    long DeliveredChars,
    int Attempt);
