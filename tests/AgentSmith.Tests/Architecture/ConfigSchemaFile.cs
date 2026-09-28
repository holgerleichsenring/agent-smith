using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Validation;
using Json.Schema;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// The shipped agentsmith.yml schema — located once, evaluated against a configuration, and
/// readable as a document so a rule can ask what a block declares.
/// </summary>
internal static class ConfigSchemaFile
{
    public static string ConfigRoot { get; } = System.IO.Path.Combine(ArchitectureSources.RepositoryRoot, "config");

    public static string Path { get; } = System.IO.Path.Combine(ConfigRoot, "agentsmith.schema.json");

    public static string ExamplePath { get; } = System.IO.Path.Combine(ConfigRoot, "agentsmith.example.yml");

    private static string Text { get; } = File.ReadAllText(Path);

    public static JsonNode Root { get; } = JsonNode.Parse(Text)!;

    private static JsonSchema Schema { get; } = JsonSchema.FromText(Text);

    /// <summary>A schema node with its <c>$ref</c> followed and its <c>allOf</c> parts merged into
    /// one property table; for a <c>oneOf</c>, the object branch.</summary>
    public static IReadOnlyDictionary<string, JsonNode> Properties(JsonNode node)
    {
        var result = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var part in Parts(node))
            foreach (var (key, value) in part["properties"]?.AsObject() ?? [])
                result[key] = value!;
        return result;
    }

    /// <summary>The schema a map's values or a list's items are held to, if the node declares one.</summary>
    public static JsonNode? Child(JsonNode node, string keyword) =>
        Parts(node).Select(part => part[keyword]).FirstOrDefault(child => child is JsonObject);

    public static IEnumerable<JsonNode> Parts(JsonNode node)
    {
        var resolved = Resolve(node);
        yield return resolved;
        foreach (var part in resolved["allOf"]?.AsArray() ?? [])
            foreach (var inner in Parts(part!)) yield return inner;
        var branch = (resolved["oneOf"] ?? resolved["anyOf"])?.AsArray()
            .Select(b => Resolve(b!)).FirstOrDefault(b => (string?)b["type"] == "object");
        if (branch is not null)
            foreach (var inner in Parts(branch)) yield return inner;
    }

    private static JsonNode Resolve(JsonNode node)
    {
        var reference = (string?)node["$ref"];
        return reference is null ? node : Resolve(Root["$defs"]![reference.Split('/')[^1]]!);
    }

    public static IReadOnlyList<string> ValidateFile(string yamlPath) =>
        Validate(File.ReadAllText(yamlPath));

    public static IReadOnlyList<string> Validate(string yaml)
    {
        var result = Schema.Evaluate(YamlAsJson.Convert(yaml),
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        // A failed oneOf branch is listed even when another branch matched; only an
        // overall failure is a finding.
        if (result.IsValid) return [];
        return [.. result.Details
            .Where(detail => !detail.IsValid && detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!
                .Select(error => $"{detail.InstanceLocation}: {error.Value}"))
            .Distinct()];
    }
}
