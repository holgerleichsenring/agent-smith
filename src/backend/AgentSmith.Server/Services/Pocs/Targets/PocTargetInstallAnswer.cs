namespace AgentSmith.Server.Services.Pocs.Targets;

/// <summary>2026-11-23-933dd: how a poc-target-install run ended — whether it succeeded, its outcome (the commit, or why
/// nothing was written) and the run.</summary>
internal sealed record PocTargetInstallAnswer(string Target, bool Succeeded, string Message, string RunId);
