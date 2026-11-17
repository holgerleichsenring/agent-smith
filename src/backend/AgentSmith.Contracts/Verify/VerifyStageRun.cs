namespace AgentSmith.Contracts.Verify;

/// <summary>
/// 2026-11-17-d6fbc: one stage a verify pass ran (or skipped) for one repository, as data — the label the declaration
/// gives it (install, build, test), the command, its exit code.
/// </summary>
public sealed record VerifyStageRun(string Stage, string Command, int ExitCode, bool Skipped);
