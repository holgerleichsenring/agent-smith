namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Inclusive numeric range for a <see cref="ConceptType.Int"/> concept.
/// Min must be ≤ Max; the loader rejects entries that violate this invariant.
/// </summary>
public sealed record ConceptIntRange(int Min, int Max);
