using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Validation;
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
/// </summary>
public sealed class ContextSchemaRule(ContextSchemaProvider schema, ContextDefectReport report)
{
    /// <summary>Everything the schema refused, unworded.</summary>
    public IReadOnlyList<ContextSchemaDefect> Defects(JsonNode? document)
    {
        var result = schema.Schema.Evaluate(
            document, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        return [.. result.Details
            .Where(detail => !detail.IsValid && detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => new ContextSchemaDefect(
                detail.InstanceLocation.ToString(),
                error.Key,
                error.Value,
                detail.EvaluationPath.ToString())))
            .Distinct()];
    }

    /// <summary>The rejection for a document judged by the schema alone.</summary>
    public string? Defect(JsonNode? document) => report.Compose(null, Defects(document));
}
