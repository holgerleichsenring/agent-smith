namespace AgentSmith.Server.Services.Preconditions;

/// <summary>2026-11-16-f3fbc: what one pass knows beside the stuck ticket — when the stored configuration last
/// changed (null when never), and the readiness answers it already read.</summary>
public sealed record PreconditionPassScope(DateTimeOffset? ConfigChangedAt, RepoReadinessPass Readiness);
