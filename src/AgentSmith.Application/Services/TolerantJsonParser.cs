using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Tolerant LLM-JSON parser. Routing-point for the four known LLM failure
/// shapes: markdown-fence wrapping, verbose prose around the JSON,
/// mid-array truncation, and empty-string-as-omitted optional fields.
/// Stateless — transient DI.
/// </summary>
public sealed class TolerantJsonParser(
    ITolerantParseTelemetry telemetry,
    ILogger<TolerantJsonParser> logger) : ITolerantJsonParser
{
    public TolerantParseResult ParseObject(string raw) => ParseSpan(raw, '{', '}');

    public TolerantParseResult ParseArray(string raw) => ParseSpan(raw, '[', ']');

    public IReadOnlyList<string> ExtractArrayObjects(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        var stripped = StripFences(raw, null);
        var literals = TolerantJsonObjectScanner.ExtractObjects(stripped).ToList();
        if (literals.Count > 0)
        {
            var detail = $"recovered {literals.Count} object literal(s)";
            logger.LogDebug("Tolerant parser: resilient fallback {Detail}", detail);
            telemetry.Record(TolerantRecoveryKind.ResilientFallback, detail);
        }
        return literals;
    }

    public string? GetStringOrNull(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in propertyNames)
        {
            if (!element.TryGetProperty(name, out var prop)) continue;
            if (prop.ValueKind != JsonValueKind.String) continue;
            var value = prop.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        return null;
    }

    private TolerantParseResult ParseSpan(string raw, char open, char close)
    {
        var diagnostics = new List<TolerantParseDiagnostic>();
        if (string.IsNullOrWhiteSpace(raw)) return Failed(diagnostics, "empty input");

        var cleaned = StripFences(raw, diagnostics);
        var extracted = ExtractSpan(cleaned, open, close, diagnostics);

        try
        {
            return new TolerantParseResult(JsonDocument.Parse(extracted), diagnostics);
        }
        catch (JsonException ex)
        {
            return Failed(diagnostics, ex.Message);
        }
    }

    private string StripFences(string raw, List<TolerantParseDiagnostic>? diagnostics)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```")) return text;
        var firstNewline = text.IndexOf('\n');
        if (firstNewline > 0) text = text[(firstNewline + 1)..];
        if (text.EndsWith("```")) text = text[..^3];
        text = text.Trim();
        Record(diagnostics, TolerantRecoveryKind.FencesStripped, "stripped triple-backtick fence");
        return text;
    }

    private string ExtractSpan(
        string text, char open, char close, List<TolerantParseDiagnostic> diagnostics)
    {
        var start = text.IndexOf(open);
        var end = text.LastIndexOf(close);
        if (start < 0 || end <= start) return text;
        if (start > 0 || end < text.Length - 1)
            Record(diagnostics, TolerantRecoveryKind.JsonExtracted,
                $"trimmed {start} char(s) prose-before, {text.Length - end - 1} char(s) prose-after");
        return text[start..(end + 1)];
    }

    private void Record(
        List<TolerantParseDiagnostic>? diagnostics, TolerantRecoveryKind kind, string detail)
    {
        diagnostics?.Add(new TolerantParseDiagnostic(kind, detail));
        logger.LogDebug("Tolerant parser: {Kind} — {Detail}", kind, detail);
        telemetry.Record(kind, detail);
    }

    private TolerantParseResult Failed(List<TolerantParseDiagnostic> diagnostics, string detail)
    {
        diagnostics.Add(new TolerantParseDiagnostic(TolerantRecoveryKind.Failed, detail));
        telemetry.Record(TolerantRecoveryKind.Failed, detail);
        return new TolerantParseResult(null, diagnostics);
    }
}
