using System.Text.Json;
using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Loop;
using Json.Schema;

namespace AgentSmith.Application.Services.Validation;

/// <summary>
/// Shared JSON-against-schema validation routine. Each output_schema validator
/// (Plan/Diff/Bootstrap) reuses this — the schema is the only thing that varies.
/// Returns ValidationResult.Failure with JSON-Pointer + rule-description error
/// strings joined by '; ' so RetryCoordinator can append a precise retry hint.
/// </summary>
internal static class SchemaValidator
{
    private const int MaxClauses = 10;
    private const string AdditionalProperties = "additionalProperties";
    private const string PropertyNotAllowed = "this property is not allowed here";

    public static ValidationResult Validate(string output, JsonSchema schema, string schemaName)
    {
        if (string.IsNullOrWhiteSpace(output))
            return ValidationResult.Invalid($"{schemaName} output is empty");

        JsonDocument document;
        try { document = JsonDocument.Parse(output); }
        catch (JsonException ex)
        {
            return ValidationResult.Invalid($"{schemaName} output is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            return Judge(schema.Evaluate(document.RootElement, ListOutput()), schema, schemaName);
        }
    }

    /// <summary>
    /// 2026-08-25-2c7c: the same evaluation over an already-parsed document, so a YAML
    /// draft crosses <see cref="YamlAsJson"/> once instead of being re-serialised to a
    /// JSON string that no longer knows which scalars were numbers.
    /// </summary>
    public static ValidationResult Validate(JsonNode? document, JsonSchema schema, string schemaName) =>
        Judge(schema.Evaluate(document, ListOutput()), schema, schemaName);

    private static EvaluationOptions ListOutput() => new() { OutputFormat = OutputFormat.List };

    private static ValidationResult Judge(EvaluationResults result, JsonSchema schema, string schemaName) =>
        result.IsValid
            ? ValidationResult.Valid()
            : ValidationResult.Invalid(FormatErrors(result, schema, schemaName));

    private static string FormatErrors(EvaluationResults results, JsonSchema schema, string schemaName)
    {
        var messages = results.Details
            .Where(Decided)
            .SelectMany(d => d.Errors!.Select(e => FormatOne(schemaName, schema, d, e)))
            .Distinct()
            .ToList();
        return messages.Count == 0
            ? $"{schemaName} output failed schema validation"
            : Bounded(messages);
    }

    /// <summary>
    /// 2026-09-20-2789: a detail whose ancestor evaluated valid decided nothing — it is the
    /// losing branch of a <c>oneOf</c> that matched, or the failing <c>if</c> of an
    /// <c>if</c>/<c>then</c> pair that therefore did not apply. Reporting it names a type the
    /// schema explicitly allows, and the false half grows with the length of the draft.
    /// </summary>
    private static bool Decided(EvaluationResults detail) =>
        !detail.IsValid && detail.Errors is { Count: > 0 } && !HasValidAncestor(detail);

    private static bool HasValidAncestor(EvaluationResults detail)
    {
        for (var ancestor = detail.Parent; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.IsValid) return true;
        return false;
    }

    private static string Bounded(IReadOnlyList<string> messages)
    {
        var shown = string.Join("; ", messages.Take(MaxClauses));
        return messages.Count <= MaxClauses
            ? shown
            : $"{shown}; (report cut at {MaxClauses} problems, {messages.Count - MaxClauses} more not shown)";
    }

    private static string FormatOne(
        string schemaName, JsonSchema schema, EvaluationResults detail, KeyValuePair<string, string> error)
    {
        var pointer = detail.InstanceLocation.ToString();
        var location = string.IsNullOrEmpty(pointer) ? schemaName : $"{schemaName}{pointer}";
        if (!IsUnknownProperty(detail, error)) return $"{location}: {error.Value}";
        return $"{location}: {PropertyNotAllowed}{SchemaAllowedProperties.Of(schema, detail)}";
    }

    /// <summary>
    /// 2026-09-20-2789: an unknown property is an <c>additionalProperties</c> path carrying an
    /// EMPTY error key — the library's wording for a <c>false</c> subschema. The path alone does
    /// not identify it: a schema-valued <c>additionalProperties</c> reports at the same path with
    /// a real error key, and there the property IS allowed, only its value is wrong.
    /// </summary>
    private static bool IsUnknownProperty(EvaluationResults detail, KeyValuePair<string, string> error) =>
        error.Key.Length == 0
        && detail.EvaluationPath.Count > 0
        && detail.EvaluationPath[^1] == AdditionalProperties;
}
