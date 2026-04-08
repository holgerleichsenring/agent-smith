namespace AgentSmith.Contracts.Models;

/// <summary>
/// Provenance metadata for a skill imported from an external source.
/// </summary>
public sealed record SkillSource(
    string Origin,
    string Version,
    string Commit,
    DateOnly Reviewed,
    string ReviewedBy);
