namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// What one declared verification command did. p0419: shared between the runner that
/// executes a command and the handler that decides what its exit code means.
/// </summary>
/// <param name="Cwd">2026-09-03-7bac: the directory the command ran in, carried so a red
/// outcome can say where it stood. A command run in the wrong place and a command that
/// is genuinely broken look identical without it.</param>
public sealed record VerifyOutcome(
    string Key, string Stage, string Command, int ExitCode, bool Skipped,
    string Output = "", string Cwd = "");
