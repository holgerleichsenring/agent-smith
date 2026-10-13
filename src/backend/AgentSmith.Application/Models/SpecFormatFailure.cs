namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-13-a347a: a declared formatter that failed at a spec commit — the repository and
/// context it ran for, the command as declared, its exit code (-1 when it could not start) and
/// the masked tail of what it said.
/// </summary>
public sealed record SpecFormatFailure(
    string Repository, string ContextName, string DeclaredCommand, int ExitCode, string OutputTail);
