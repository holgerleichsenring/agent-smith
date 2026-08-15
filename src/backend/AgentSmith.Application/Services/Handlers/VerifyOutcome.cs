namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// What one declared verification command did. p0419: shared between the runner that
/// executes a command and the handler that decides what its exit code means.
/// </summary>
public sealed record VerifyOutcome(
    string Key, string Stage, string Command, int ExitCode, bool Skipped,
    string Output = "", bool NotWorseThanBaseline = false);
