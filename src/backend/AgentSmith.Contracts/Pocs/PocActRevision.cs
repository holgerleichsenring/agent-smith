namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-c81ed: what a rollback reads of the revision it rolls to — its pushed digest, the source commit
/// its image is tagged with, and how its latest rollout ended (PocRolloutStates, null while none ended).</summary>
public sealed record PocActRevision(int Number, string? Digest, string? CommitSha, string? RolloutState);
