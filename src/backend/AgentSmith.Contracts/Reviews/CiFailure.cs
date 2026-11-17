namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: one failed check, job or stage of a red CI — its name as the host shows it, the link to its full
/// log or result, and a bounded excerpt: the host's summary and annotations, then the end of its log.
/// </summary>
public sealed record CiFailure(string Name, string? Url, string Excerpt);
