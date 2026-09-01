using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Validation;
using Json.Pointer;
using Json.Schema;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-25-c9c7: judges the context document about to be written against
/// <c>context.schema.json</c> and returns what the schema refused.
/// <para>
/// 2026-08-26-167c: it returns EVERY defect, not the first three, and does not word
/// them — <see cref="ContextDefectReport"/> does, so that one rejection can carry the
/// image rule's defect and the schema's together and be bounded as a whole.
/// </para>
/// <para>
/// 2026-09-01-0f80: with ONE exception, because for one refusal there is no wording to
/// pass on. A key the schema does not declare is refused by <c>additionalProperties</c>,
/// whose schema node is the BOOLEAN false — it names no keyword and carries no
/// description, so all the report would have is "All values fail against the false
/// schema", which names neither the field nor where it sits. The offending key is still
/// in hand here, so that refusal is worded here.
/// </para>
/// </summary>
public sealed class ContextSchemaRule(ContextSchemaProvider schema, ContextDefectReport report)
{
    private const string ClosedObject = "additionalProperties";

    /// <summary>Everything the schema refused, unworded.</summary>
    public IReadOnlyList<ContextSchemaDefect> Defects(JsonNode? document)
    {
        var result = schema.Schema.Evaluate(
            document, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        return [.. result.Details
            .Where(detail => !detail.IsValid && detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => Refusal(detail, error)))
            .Distinct()];
    }

    /// <summary>The rejection for a document judged by the schema alone.</summary>
    public string? Defect(JsonNode? document) => report.Compose(null, Defects(document));

    private static ContextSchemaDefect Refusal(
        EvaluationResults detail, KeyValuePair<string, string> error)
    {
        var location = detail.InstanceLocation;
        var path = detail.EvaluationPath;
        return IsUndeclaredField(error.Key, location, path)
            ? new ContextSchemaDefect(location.ToString(), ClosedObject,
                $"\"{location[^1]}\" is not a field this schema declares", path.ToString())
            : new ContextSchemaDefect(location.ToString(), error.Key, error.Value, path.ToString());
    }

    // The boolean schema names no keyword, so an empty error key under an
    // additionalProperties path is the closed object refusing a key it does not declare.
    private static bool IsUndeclaredField(
        string keyword, JsonPointer location, JsonPointer path) =>
        keyword.Length == 0 && location.Count > 0
        && path.Count > 0 && path[^1] == ClosedObject;
}
