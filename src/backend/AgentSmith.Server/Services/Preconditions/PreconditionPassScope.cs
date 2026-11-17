namespace AgentSmith.Server.Services.Preconditions;

/// <summary>2026-11-16-f3fbc: what one pass knows beside the stuck ticket — whether the config epoch started it, and
/// the readiness answers it already read.</summary>
public sealed record PreconditionPassScope(bool EpochStart, RepoReadinessPass Readiness);
