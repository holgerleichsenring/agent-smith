using System.Text.Json.Nodes;
using Json.Schema;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-056d: the shipped context schema — located once, evaluated against a
/// context file, and readable as a document so a rule can ask what it declares.
/// </summary>
internal static class ContextSchemaFile
{
    public static string Path { get; } =
        System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "context.schema.json");

    public static string ContextPath { get; } = System.IO.Path.Combine(
        ArchitectureSources.AgentSmithRoot, "contexts", "default", "context.yaml");

    public static string TemplatePath { get; } =
        System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "template.context.yaml");

    private static string Text { get; } = File.ReadAllText(Path);

    public static JsonNode Root { get; } = JsonNode.Parse(Text)!;

    private static JsonSchema Schema { get; } = JsonSchema.FromText(Text);

    /// <summary>The keys declared under a dotted block path — "" for the document root.</summary>
    public static IReadOnlyCollection<string> DeclaredKeys(string blockPath)
    {
        var node = Root["properties"]!;
        foreach (var segment in blockPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            node = node[segment]!["properties"]!;
        return [.. node.AsObject().Select(pair => pair.Key)];
    }

    public static IReadOnlyList<string> ValidateFile(string yamlPath) =>
        Validate(File.ReadAllText(yamlPath));

    public static IReadOnlyList<string> Validate(string yaml)
    {
        var result = Schema.Evaluate(YamlAsJson.Convert(yaml),
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        return [.. result.Details
            .Where(detail => !detail.IsValid && detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!
                .Select(error => $"{detail.InstanceLocation}: {error.Value}"))
            .Distinct()];
    }
}
