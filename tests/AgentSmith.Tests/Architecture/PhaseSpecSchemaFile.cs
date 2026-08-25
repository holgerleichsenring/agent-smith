using System.Text.Json.Nodes;
using Json.Schema;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521: the shipped phase-spec schema — located once, readable as a document, and
/// evaluatable against a phase file.
/// <para>
/// This is the same file <c>AgentSmith.Application.csproj</c> embeds and the deployed
/// server evaluates every model-authored draft against, so its limits are a PRODUCT
/// CONTRACT and not this repository's taste. The repo's own convention lives in
/// <see cref="PhaseNameRuleTests"/>.
/// </para>
/// </summary>
internal static class PhaseSpecSchemaFile
{
    public static string Path { get; } =
        System.IO.Path.Combine(ArchitectureSources.AgentSmithRoot, "phase-spec.schema.json");

    private static string Text { get; } = File.ReadAllText(Path);

    private static JsonNode Root { get; } = JsonNode.Parse(Text)!;

    private static JsonSchema Schema { get; } = JsonSchema.FromText(Text);

    /// <summary>The schema's own goal limit, read from the schema and never restated.</summary>
    public static int GoalMaxLength { get; } =
        Root["properties"]!["goal"]!["maxLength"]!.GetValue<int>();

    /// <summary>
    /// The errors that actually make the document invalid.
    /// <para>
    /// A FLAT reading cannot answer that. The schema uses <c>oneOf</c> in three places, and
    /// a flat list reports every branch that did NOT match even when one did — so a
    /// perfectly valid <c>requires:</c> list is reported as "should be string". Reading the
    /// hierarchy and descending only into nodes that are themselves invalid is what makes
    /// the difference between a rule and a noise generator.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Validate(JsonNode document)
    {
        var result = Schema.Evaluate(
            document, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        return result.IsValid ? [] : [.. Errors(result).Distinct()];
    }

    private static IEnumerable<string> Errors(EvaluationResults node)
    {
        if (node.IsValid) yield break;

        foreach (var error in node.Errors ?? new Dictionary<string, string>())
            yield return $"{node.InstanceLocation}: {error.Value}";

        foreach (var error in node.Details.SelectMany(Errors))
            yield return error;
    }
}
