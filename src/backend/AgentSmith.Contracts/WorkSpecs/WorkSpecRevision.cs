namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: the header of one revision. Re-entry never regenerates the spec — it
/// reads the last revision and writes a new one naming its CAUSE ("comment on
/// ticket", "reviewer edit in PR #N", "resume", "master revision"). The transform
/// is an LLM and will never be deterministic; reproducibility comes from not
/// repeating it, not from expecting the same answer twice.
/// </summary>
public sealed record WorkSpecRevision(int Number, string Cause, DateTimeOffset At);
