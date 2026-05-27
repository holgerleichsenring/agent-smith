namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Type a concept declares for its values. Bool / Int / Enum constrain values at
/// write time (typo-safety). String accepts any free-form value — used for concepts
/// where the value vocabulary lives outside the .NET catalog (e.g. p0155's
/// <c>project_language</c>, where the project-analyzer LLM owns the canonical-slug
/// rule and the dispatcher fails loud on no-match). Use Enum by default; reach for
/// String only when the value space cannot be enumerated upfront.
/// </summary>
public enum ConceptType
{
    /// <summary>Presence flag. Default when unset is <c>false</c>.</summary>
    Bool,

    /// <summary>Bounded numeric. Range declared via <see cref="ConceptIntRange"/>; default when unset is <c>0</c>.</summary>
    Int,

    /// <summary>Closed-set string. Values declared via the concept's EnumValues list; default when unset is the first value.</summary>
    Enum,

    /// <summary>Free-form string with no closed value set. Default when unset is <c>""</c>. Reserved for concepts whose vocabulary is owned by the LLM (analyzer slugs) rather than the C# catalog.</summary>
    String
}
