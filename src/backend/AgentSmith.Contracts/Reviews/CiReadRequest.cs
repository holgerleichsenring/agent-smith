namespace AgentSmith.Contracts.Reviews;

/// <summary>
/// 2026-11-17-3435a: how a CI read is bounded — the head it is read for (null: the pull request's current head), how
/// many characters each failure's excerpt keeps, and how many failed checks have their logs read (0: names and links
/// only, as for a head already stored red). Both bounds come from the project's merge block.
/// </summary>
public sealed record CiReadRequest(string? HeadSha, int ExcerptChars, int FailuresRead);
