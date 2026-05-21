namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Flat lookup of all declared concepts. Loaded from skills/concept-vocabulary.yaml
/// at startup; operators may extend the file. Duplicate names are an error caught
/// by the loader. <see cref="GetDefault"/> returns the per-type default value
/// used when an activation expression reads an unset concept.
/// </summary>
public sealed record ConceptVocabulary(IReadOnlyDictionary<string, ProjectConcept> Concepts)
{
    public static ConceptVocabulary Empty { get; } =
        new ConceptVocabulary(new Dictionary<string, ProjectConcept>());

    public bool TryGet(string name, out ProjectConcept concept)
    {
        if (Concepts.TryGetValue(name, out var c))
        {
            concept = c;
            return true;
        }
        concept = null!;
        return false;
    }

    /// <summary>
    /// Typed default for an unset concept: <c>false</c> for Bool, <c>0</c> for Int,
    /// the first declared enum value for Enum. Throws <see cref="KeyNotFoundException"/>
    /// when the name is not declared — writers should fail loud on typos rather than
    /// silently see a default.
    /// </summary>
    public object GetDefault(string conceptName)
    {
        if (!Concepts.TryGetValue(conceptName, out var concept))
            throw new KeyNotFoundException(
                $"Concept '{conceptName}' is not declared in concept-vocabulary.yaml");

        return concept.Type switch
        {
            ConceptType.Bool => false,
            ConceptType.Int => 0,
            ConceptType.Enum => concept.EnumValues![0],
            ConceptType.String => string.Empty,
            _ => throw new InvalidOperationException($"Unknown ConceptType {concept.Type}")
        };
    }
}
