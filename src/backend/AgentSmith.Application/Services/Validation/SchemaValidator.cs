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
            return Judge(schema.Evaluate(document.RootElement, ListOutput()), schemaName);
        }
    }

    /// <summary>
    /// 2026-08-25-2c7c: the same evaluation over an already-parsed document, so a YAML
    /// draft crosses <see cref="YamlAsJson"/> once instead of being re-serialised to a
    /// JSON string that no longer knows which scalars were numbers.
    /// </summary>
    public static ValidationResult Validate(JsonNode? document, JsonSchema schema, string schemaName) =>
        Judge(schema.Evaluate(document, ListOutput()), schemaName);

    private static EvaluationOptions ListOutput() => new() { OutputFormat = OutputFormat.List };

    private static ValidationResult Judge(EvaluationResults result, string schemaName) =>
        result.IsValid
            ? ValidationResult.Valid()
            : ValidationResult.Invalid(FormatErrors(result, schemaName));

    private static string FormatErrors(EvaluationResults results, string schemaName)
    {
        var messages = results.Details
            .Where(d => !d.IsValid && d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => FormatOne(schemaName, d.InstanceLocation.ToString(), e.Value)))
            .Distinct()
            .Take(10)
            .ToList();
        return messages.Count == 0
            ? $"{schemaName} output failed schema validation"
            : string.Join("; ", messages);
    }

    private static string FormatOne(string schemaName, string pointer, string rule)
    {
        var location = string.IsNullOrEmpty(pointer) ? schemaName : $"{schemaName}{pointer}";
        return $"{location}: {rule}";
    }
}
