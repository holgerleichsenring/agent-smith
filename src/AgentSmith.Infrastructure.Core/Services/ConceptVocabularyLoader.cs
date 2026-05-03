using AgentSmith.Contracts.Models.Skills;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Loads skills/concept-vocabulary.yaml — the controlled vocabulary referenced by skill
/// activation positive keys. Three sections (project_concepts, change_signals, run_context)
/// flatten into a single namespace; duplicate keys across sections are an error.
/// </summary>
public sealed class ConceptVocabularyLoader(ILogger<ConceptVocabularyLoader> logger)
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Loads skills/concept-vocabulary.yaml from <paramref name="skillsDirectory"/>. Returns
    /// <see cref="ConceptVocabulary.Empty"/> if the file is missing — callers receive a warning,
    /// not an exception, so deployments without a vocabulary file still boot.
    /// </summary>
    public ConceptVocabulary Load(string skillsDirectory)
    {
        var path = Path.Combine(skillsDirectory, "concept-vocabulary.yaml");
        if (!File.Exists(path))
        {
            logger.LogWarning("concept-vocabulary.yaml not found at {Path} — skills load with empty vocabulary", path);
            return ConceptVocabulary.Empty;
        }

        var yaml = File.ReadAllText(path);
        var raw = Deserializer.Deserialize<RawConceptVocabularyFile>(yaml);
        if (raw?.Concepts is null)
        {
            logger.LogWarning("concept-vocabulary.yaml at {Path} is empty or malformed", path);
            return ConceptVocabulary.Empty;
        }

        var lookup = new Dictionary<string, ProjectConcept>(StringComparer.Ordinal);
        Add(raw.Concepts.ProjectConcepts, "project_concepts", lookup, path);
        Add(raw.Concepts.ChangeSignals, "change_signals", lookup, path);
        Add(raw.Concepts.RunContext, "run_context", lookup, path);

        logger.LogInformation(
            "Loaded {Count} concepts from {Path} ({P} project, {C} change, {R} run)",
            lookup.Count, path,
            raw.Concepts.ProjectConcepts?.Count ?? 0,
            raw.Concepts.ChangeSignals?.Count ?? 0,
            raw.Concepts.RunContext?.Count ?? 0);

        return new ConceptVocabulary(lookup);
    }

    private void Add(
        List<RawConcept>? entries,
        string section,
        Dictionary<string, ProjectConcept> lookup,
        string sourcePath)
    {
        if (entries is null) return;
        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Key)) continue;
            if (lookup.TryGetValue(entry.Key, out var existing))
            {
                logger.LogError(
                    "concept-vocabulary.yaml at {Path}: duplicate key '{Key}' in section '{Section}' (already declared in '{ExistingSection}')",
                    sourcePath, entry.Key, section, existing.Section);
                throw new InvalidOperationException(
                    $"concept-vocabulary.yaml has duplicate key '{entry.Key}' across sections '{existing.Section}' and '{section}'");
            }
            lookup[entry.Key] = new ProjectConcept(entry.Key, entry.Desc ?? string.Empty, section);
        }
    }

    private sealed class RawConceptVocabularyFile
    {
        public RawConceptSections? Concepts { get; set; }
    }

    private sealed class RawConceptSections
    {
        public List<RawConcept>? ProjectConcepts { get; set; }
        public List<RawConcept>? ChangeSignals { get; set; }
        public List<RawConcept>? RunContext { get; set; }
    }

    private sealed class RawConcept
    {
        public string Key { get; set; } = string.Empty;
        public string? Desc { get; set; }
    }
}
