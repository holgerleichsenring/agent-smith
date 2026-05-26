using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Tolerant parser for LLM-emitted JSON. Strips ```json fences and surrounding
/// prose, recovers complete object literals from truncated arrays, and unifies
/// empty-string-vs-omitted optional fields. Direct webhook / ticket-API JSON
/// has stable shape from real APIs and should keep using plain
/// JsonSerializer.Deserialize — this seam is only for probabilistic generator
/// output.
/// </summary>
public interface ITolerantJsonParser
{
    /// <summary>
    /// Tolerantly parses <paramref name="raw"/> as a JSON object. Strips
    /// fences and prose around the JSON; on JsonException returns a result
    /// whose Document is null with diagnostics describing the failure.
    /// </summary>
    TolerantParseResult ParseObject(string raw);

    /// <summary>
    /// Tolerantly parses <paramref name="raw"/> as a JSON array. Strips
    /// fences and prose around the JSON, extracts the outermost `[...]` span,
    /// and returns its document. Empty/zero-length arrays produce a result
    /// with a non-null Document whose root is an empty array — callers that
    /// treat zero elements as "no signal" handle that themselves.
    /// </summary>
    TolerantParseResult ParseArray(string raw);

    /// <summary>
    /// Tolerantly extracts complete `{...}` object literals from a possibly
    /// truncated JSON array. The trailing partial object — when the response
    /// hit max_output_tokens mid-array — is silently dropped. Each returned
    /// literal is independently parseable as a JsonDocument.
    /// </summary>
    IReadOnlyList<string> ExtractArrayObjects(string raw);

    /// <summary>
    /// Returns the first matching property's string value with empty/whitespace
    /// folded to null, or null when no property matches. Replaces the
    /// per-handler NullIfEmpty defence: LLMs often emit "" for fields they
    /// have no value for and downstream code treats that as a present-but-empty
    /// value instead of "this field was not provided".
    /// </summary>
    string? GetStringOrNull(JsonElement element, params string[] propertyNames);
}
