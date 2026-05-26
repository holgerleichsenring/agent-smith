namespace AgentSmith.Contracts.Models;

/// <summary>
/// A skill discovered from an external source, pending evaluation and approval.
/// </summary>
public sealed record SkillCandidate(
    string Name,
    string Description,
    string SourceUrl,
    string Content,
    string? Version,
    string? Commit);
