using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Validation;
using Json.Schema;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-25-c9c7: judges the context document about to be written against
/// <c>context.schema.json</c> and returns the defect, or null when it validates.
/// <para>
/// The message carries a JSON Pointer into the document AND the rule that was
/// broken, read back out of the schema — the model never sees the schema file,
/// so "should match one of the enum values" without the values is an invitation
/// to guess, and guessing is what turns a rejection into a loop.
/// </para>
/// </summary>
public sealed class ContextSchemaRule(ContextSchemaProvider schema)
{
    private const int MaxDefects = 3;
    private const int MaxRuleLength = 400;

    public string? Defect(JsonNode? document)
    {
        var result = schema.Schema.Evaluate(
            document, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return null;

        var defects = result.Details
            .Where(detail => !detail.IsValid && detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => Describe(detail, error.Key, error.Value)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(message => message, StringComparer.Ordinal)
            .Take(MaxDefects)
            .ToList();

        return defects.Count == 0
            ? "the document does not validate against context.schema.json"
            : string.Join("; ", defects);
    }

    private string Describe(EvaluationResults detail, string keyword, string message)
    {
        var location = detail.InstanceLocation.ToString();
        if (location.Length == 0) location = "/";
        var rule = Rule(detail.EvaluationPath.ToString(), keyword);
        return rule is null
            ? $"{location}: {message}"
            : $"{location}: {message} (schema {keyword}: {rule})";
    }

    // The evaluation path is a JSON Pointer into the schema document itself, so the
    // broken rule can be quoted verbatim next to the instance that broke it.
    private string? Rule(string evaluationPath, string keyword)
    {
        var node = schema.Document;
        foreach (var segment in evaluationPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node?[Unescape(segment)];
            if (node is null) return null;
        }
        var rule = node?[keyword]?.ToJsonString();
        return rule is null || rule.Length > MaxRuleLength ? null : rule;
    }

    private static string Unescape(string segment) =>
        segment.Replace("~1", "/", StringComparison.Ordinal)
               .Replace("~0", "~", StringComparison.Ordinal);
}
