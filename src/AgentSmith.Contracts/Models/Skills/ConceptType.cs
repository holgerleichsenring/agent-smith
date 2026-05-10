namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Type a concept declares for its values. Closed set: no string-typed concepts —
/// free strings would let activation expressions like <c>language = "csharp"</c>
/// drift undetected on typos. Enum forces the writer to declare the value range
/// upfront in concept-vocabulary.yaml.
/// </summary>
public enum ConceptType
{
    /// <summary>Presence flag. Default when unset is <c>false</c>.</summary>
    Bool,

    /// <summary>Bounded numeric. Range declared via <see cref="ConceptIntRange"/>; default when unset is <c>0</c>.</summary>
    Int,

    /// <summary>Closed-set string. Values declared via the concept's EnumValues list; default when unset is the first value.</summary>
    Enum
}
