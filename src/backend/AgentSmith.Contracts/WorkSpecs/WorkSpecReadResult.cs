namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: an existing spec found on the ticket branch, with the sha of the last
/// commit that touched its path. The caller compares that sha against the
/// pointer this system recorded to tell its OWN last revision apart from a
/// reviewer's edit — the cause the next revision names.
/// </summary>
public sealed record WorkSpecReadResult(WorkSpecArtifact Artifact, string LastCommitSha);
