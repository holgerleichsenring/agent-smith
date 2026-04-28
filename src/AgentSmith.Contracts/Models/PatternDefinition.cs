namespace AgentSmith.Contracts.Models;

/// <summary>
/// A single regex pattern loaded from a pattern definition YAML file.
/// </summary>
public sealed record PatternDefinition(
    string Id,
    string Category,
    string Regex,
    string Severity,
    int Confidence,
    string Title,
    string Description,
    string? Cwe,
    string? Provider = null,
    string? RevocationUrl = null);

/// <summary>
/// A collection of patterns loaded from a YAML file.
/// </summary>
public sealed record PatternDefinitionFile(
    string Name,
    IReadOnlyList<PatternDefinition> Patterns);
