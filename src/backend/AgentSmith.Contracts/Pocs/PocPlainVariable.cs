using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: one plain variable as Poc.PlainVariablesJson holds it.</summary>
public sealed record PocPlainVariable(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("set_by")] string SetBy,
    [property: JsonPropertyName("set_at")] DateTimeOffset SetAt);
