using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Loads pattern definition YAML files from a directory and converts them
/// into strongly-typed <see cref="PatternDefinition"/> objects.
/// </summary>
public sealed class PatternDefinitionLoader(ILogger<PatternDefinitionLoader> logger)
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyList<PatternDefinition> LoadFromDirectory(string patternsDirectory)
    {
        if (!Directory.Exists(patternsDirectory))
        {
            logger.LogWarning("Patterns directory not found: {Directory}", patternsDirectory);
            return [];
        }

        var files = Directory.GetFiles(patternsDirectory, "*.yaml")
            .Concat(Directory.GetFiles(patternsDirectory, "*.yml"))
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0)
        {
            logger.LogWarning("No pattern YAML files found in {Directory}", patternsDirectory);
            return [];
        }

        var definitions = new List<PatternDefinition>();

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var parsed = Deserializer.Deserialize<PatternFileDto>(yaml);

                if (parsed?.Patterns is null || parsed.Patterns.Count == 0)
                {
                    logger.LogWarning("No patterns found in {File}", file);
                    continue;
                }

                var category = parsed.Name ?? Path.GetFileNameWithoutExtension(file);

                foreach (var p in parsed.Patterns)
                {
                    definitions.Add(new PatternDefinition(
                        Id: p.Id ?? "unknown",
                        Category: category,
                        Regex: p.Regex ?? string.Empty,
                        Severity: p.Severity ?? "info",
                        Confidence: p.Confidence,
                        Title: p.Title ?? p.Id ?? "Untitled",
                        Description: p.Description ?? string.Empty,
                        Cwe: p.Cwe,
                        Provider: p.Provider,
                        RevocationUrl: p.RevocationUrl));
                }

                logger.LogDebug("Loaded {Count} patterns from {File}", parsed.Patterns.Count, file);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to parse pattern file {File}", file);
            }
        }

        logger.LogInformation("Loaded {Total} pattern definitions from {Files} files",
            definitions.Count, files.Count);

        return definitions;
    }

    // ReSharper disable ClassNeverInstantiated.Local
    private sealed class PatternFileDto
    {
        public string? Name { get; set; }
        public List<PatternEntryDto>? Patterns { get; set; }
    }

    private sealed class PatternEntryDto
    {
        public string? Id { get; set; }
        public string? Regex { get; set; }
        public string? Severity { get; set; }
        public int Confidence { get; set; }
        public string? Cwe { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Provider { get; set; }
        public string? RevocationUrl { get; set; }
    }
    // ReSharper restore ClassNeverInstantiated.Local
}
