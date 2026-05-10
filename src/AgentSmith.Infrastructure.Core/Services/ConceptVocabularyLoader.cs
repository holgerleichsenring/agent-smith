using AgentSmith.Contracts.Models.Skills;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Loads skills/concept-vocabulary.yaml — the typed concept vocabulary referenced
/// by skill activation positive keys. Flat list of typed concepts (bool/int/enum).
/// Boot fails loudly on legacy three-section shape, missing required fields, or
/// type-incompatible attributes; missing file is a warning, not a failure.
/// </summary>
public sealed class ConceptVocabularyLoader(ILogger<ConceptVocabularyLoader> logger)
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public ConceptVocabulary Load(string skillsDirectory)
    {
        var path = Path.Combine(skillsDirectory, "concept-vocabulary.yaml");
        if (!File.Exists(path))
        {
            logger.LogWarning("concept-vocabulary.yaml not found at {Path} — skills load with empty vocabulary", path);
            return ConceptVocabulary.Empty;
        }

        var yaml = File.ReadAllText(path);
        RejectLegacyShape(yaml, path);

        var raw = Deserializer.Deserialize<RawFile>(yaml);
        if (raw?.Concepts is null)
            throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path} is empty or missing the top-level 'concepts:' list");

        var lookup = BuildLookup(raw.Concepts, path);
        logger.LogInformation("Loaded {Count} concepts from {Path}", lookup.Count, path);
        return new ConceptVocabulary(lookup);
    }

    private static void RejectLegacyShape(string yaml, string path)
    {
        var hasLegacySection = yaml.Contains("\n  project_concepts:") ||
                                yaml.Contains("\n  change_signals:") ||
                                yaml.Contains("\n  run_context:");
        if (!hasLegacySection) return;
        throw new InvalidOperationException(
            $"concept-vocabulary.yaml at {path} is in the legacy three-section shape (project_concepts/change_signals/run_context). " +
            "Migrate to the flat typed schema: concepts: [{name, type, description, enum_values?, int_range?, writers?}]. " +
            "See p0125a phase spec for details.");
    }

    private static Dictionary<string, ProjectConcept> BuildLookup(List<RawConcept> entries, string path)
    {
        var lookup = new Dictionary<string, ProjectConcept>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var concept = BuildConcept(entry, path);
            if (lookup.ContainsKey(concept.Name))
                throw new InvalidOperationException(
                    $"concept-vocabulary.yaml at {path}: duplicate concept name '{concept.Name}'");
            lookup[concept.Name] = concept;
        }
        return lookup;
    }

    private static ProjectConcept BuildConcept(RawConcept entry, string path)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
            throw new InvalidOperationException($"concept-vocabulary.yaml at {path}: entry is missing 'name'");
        var type = ParseType(entry.Type, entry.Name, path);
        ValidateAttributes(entry, type, path);

        var enumValues = type == ConceptType.Enum ? entry.EnumValues!.AsReadOnly() : null;
        var intRange = type == ConceptType.Int ? new ConceptIntRange(entry.IntRange![0], entry.IntRange[1]) : null;
        var writers = (entry.Writers ?? []).AsReadOnly();
        return new ProjectConcept(entry.Name, entry.Description ?? string.Empty, type, enumValues, intRange, writers);
    }

    private static ConceptType ParseType(string? rawType, string conceptName, string path) =>
        rawType?.ToLowerInvariant() switch
        {
            "bool" => ConceptType.Bool,
            "int" => ConceptType.Int,
            "enum" => ConceptType.Enum,
            null or "" => throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path}: entry '{conceptName}' is missing 'type' (must be bool, int, or enum)"),
            _ => throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path}: entry '{conceptName}' has unknown type '{rawType}' (must be bool, int, or enum)")
        };

    private static void ValidateAttributes(RawConcept entry, ConceptType type, string path)
    {
        var hasEnumValues = entry.EnumValues is { Count: > 0 };
        var hasIntRange = entry.IntRange is { Count: 2 };
        if (type == ConceptType.Enum && !hasEnumValues)
            throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path}: enum concept '{entry.Name}' must declare a non-empty 'enum_values' list");
        if (type != ConceptType.Enum && hasEnumValues)
            throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path}: concept '{entry.Name}' has 'enum_values' but type is '{type.ToString().ToLowerInvariant()}' (only enum concepts may declare enum_values)");
        if (type == ConceptType.Int && !hasIntRange)
            throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path}: int concept '{entry.Name}' must declare 'int_range: [min, max]'");
        if (type != ConceptType.Int && hasIntRange)
            throw new InvalidOperationException(
                $"concept-vocabulary.yaml at {path}: concept '{entry.Name}' has 'int_range' but type is '{type.ToString().ToLowerInvariant()}' (only int concepts may declare int_range)");
    }

    private sealed class RawFile
    {
        public List<RawConcept>? Concepts { get; set; }
    }

    private sealed class RawConcept
    {
        public string Name { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Description { get; set; }
        public List<string>? EnumValues { get; set; }
        public List<int>? IntRange { get; set; }
        public List<string>? Writers { get; set; }
    }
}
