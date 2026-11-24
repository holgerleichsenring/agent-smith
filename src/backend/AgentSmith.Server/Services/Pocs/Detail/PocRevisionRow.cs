namespace AgentSmith.Server.Services.Pocs.Detail;

/// <summary>2026-11-23-c81ed: one revision on a PoC's detail page — its state, pushed digest, how its latest rollout ended,
/// the run that took it in, its source commit, whether the caller may roll back to it, and whether the row runs it.</summary>
internal sealed record PocRevisionRow(
    int Number, string State, string? Digest, string? RolloutState, DateTimeOffset? RolloutAt, string? RunId, string? CommitSha,
    bool MayRollBack, bool Current);
