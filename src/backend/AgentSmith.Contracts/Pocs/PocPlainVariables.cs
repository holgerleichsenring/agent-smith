using System.Text.Json;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: Poc.PlainVariablesJson read and written — a JSON array ordered by name; null or empty
/// holds none.</summary>
public static class PocPlainVariables
{
    public static IReadOnlyList<PocPlainVariable> Parse(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<PocPlainVariable>>(json) ?? [];

    /// <summary>The column's value: null when none is left.</summary>
    public static string? ToJson(IEnumerable<PocPlainVariable> variables)
    {
        var ordered = variables.OrderBy(v => v.Name, StringComparer.Ordinal).ToList();
        return ordered.Count == 0 ? null : JsonSerializer.Serialize(ordered);
    }
}
