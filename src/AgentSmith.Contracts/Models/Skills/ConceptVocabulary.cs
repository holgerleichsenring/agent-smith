namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// Flat lookup of all concepts referenced by skill activation positive keys.
/// Loaded from skills/concept-vocabulary.yaml at startup. Operators may extend.
/// Duplicate keys across sections are an error caught by the loader.
/// </summary>
public sealed record ConceptVocabulary(IReadOnlyDictionary<string, ProjectConcept> Concepts)
{
    public static ConceptVocabulary Empty { get; } =
        new ConceptVocabulary(new Dictionary<string, ProjectConcept>());

    public bool TryGet(string key, out ProjectConcept concept)
    {
        if (Concepts.TryGetValue(key, out var c))
        {
            concept = c;
            return true;
        }
        concept = null!;
        return false;
    }
}
