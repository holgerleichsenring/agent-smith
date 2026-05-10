namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Single entry in the controlled concept vocabulary referenced by skill
/// activation positive keys and (from p0125b) by activates_when expressions.
/// Invariants enforced by the loader:
/// EnumValues is non-null and non-empty iff Type is <see cref="ConceptType.Enum"/>;
/// IntRange is non-null iff Type is <see cref="ConceptType.Int"/>.
/// Writers lists handler class names allowed to publish this concept (validated
/// against the IConceptWriter registry in p0125d; informational in p0125a).
/// </summary>
public sealed record ProjectConcept(
    string Name,
    string Description,
    ConceptType Type,
    IReadOnlyList<string>? EnumValues,
    ConceptIntRange? IntRange,
    IReadOnlyList<string> Writers);
