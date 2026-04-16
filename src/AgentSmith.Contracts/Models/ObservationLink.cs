namespace AgentSmith.Contracts.Models;

/// <summary>
/// A relationship between two observations, detected by the convergence check.
/// </summary>
public sealed record ObservationLink(
    int ObservationId,
    int RelatedObservationId,
    ObservationRelationship Relationship);

/// <summary>
/// The type of relationship between two observations.
/// </summary>
public enum ObservationRelationship
{
    Duplicates,
    Contradicts,
    DependsOn,
    Extends
}
