namespace AgentSmith.SkillsPackaging;

/// <summary>
/// p0325: one build-breaking problem found in the skills tarball — the
/// offending master (or the tarball itself) plus a human-readable reason.
/// </summary>
public sealed record MasterDescriptionViolation(string Master, string Reason);
