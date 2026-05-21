using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;

namespace AgentSmith.Infrastructure.Services.Activation;

/// <summary>
/// PipelineContext-backed implementation of <see cref="IRunStateConcepts"/>. Concept
/// values are stored under <see cref="ContextKeys.ConceptValues"/> so they don't
/// collide with freeform run-state. Each Get/Set first looks up the concept declaration
/// via the supplied vocabulary; undeclared concepts trigger
/// <see cref="KeyNotFoundException"/> at the call site.
/// </summary>
public sealed class PipelineContextRunStateConcepts : IRunStateConcepts
{
    private readonly PipelineContext _context;
    private readonly ConceptVocabulary _vocabulary;

    public PipelineContextRunStateConcepts(PipelineContext context, ConceptVocabulary vocabulary)
    {
        _context = context;
        _vocabulary = vocabulary;
    }

    public bool GetBool(string name) => (bool)Get(name, ConceptType.Bool, "bool");

    public int GetInt(string name) => (int)Get(name, ConceptType.Int, "int");

    public string GetEnum(string name) => (string)Get(name, ConceptType.Enum, "string");

    public string GetString(string name)
    {
        if (!_vocabulary.TryGet(name, out var concept))
            throw new KeyNotFoundException(
                $"Concept '{name}' is not declared in concept-vocabulary.yaml.");
        if (concept.Type != ConceptType.Enum && concept.Type != ConceptType.String)
            throw new ConceptTypeMismatchException(name, concept.Type, "string");
        var values = GetOrCreateValues();
        return values.TryGetValue(name, out var stored)
            ? (string)stored
            : (string)_vocabulary.GetDefault(name);
    }

    public void SetBool(string name, bool value)
    {
        EnsureType(name, ConceptType.Bool, "bool");
        StoreValue(name, value);
    }

    public void SetInt(string name, int value)
    {
        var concept = EnsureType(name, ConceptType.Int, "int");
        var range = concept.IntRange;
        if (range is not null && (value < range.Min || value > range.Max))
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Concept '{name}' value {value} is outside the declared range [{range.Min}, {range.Max}].");
        StoreValue(name, value);
    }

    public void SetEnum(string name, string value)
    {
        var concept = EnsureType(name, ConceptType.Enum, "string");
        var allowed = concept.EnumValues!;
        if (!allowed.Contains(value))
            throw new ArgumentException(
                $"Concept '{name}' value '{value}' is not in the declared enum_values [{string.Join(", ", allowed)}].",
                nameof(value));
        StoreValue(name, value);
    }

    public void SetString(string name, string value)
    {
        EnsureType(name, ConceptType.String, "string");
        StoreValue(name, value);
    }

    private object Get(string name, ConceptType expectedType, string attemptedTypeName)
    {
        EnsureType(name, expectedType, attemptedTypeName);
        var values = GetOrCreateValues();
        return values.TryGetValue(name, out var stored) ? stored : _vocabulary.GetDefault(name);
    }

    private ProjectConcept EnsureType(string name, ConceptType expectedType, string attemptedTypeName)
    {
        if (!_vocabulary.TryGet(name, out var concept))
            throw new KeyNotFoundException(
                $"Concept '{name}' is not declared in concept-vocabulary.yaml.");
        if (concept.Type != expectedType)
            throw new ConceptTypeMismatchException(name, concept.Type, attemptedTypeName);
        return concept;
    }

    private void StoreValue(string name, object value)
    {
        var values = GetOrCreateValues();
        values[name] = value;
        _context.Set(ContextKeys.ConceptValues, values);
    }

    private Dictionary<string, object> GetOrCreateValues()
    {
        if (_context.TryGet<Dictionary<string, object>>(ContextKeys.ConceptValues, out var existing) && existing is not null)
            return existing;
        var fresh = new Dictionary<string, object>();
        _context.Set(ContextKeys.ConceptValues, fresh);
        return fresh;
    }
}
