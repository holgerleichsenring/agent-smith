using System.Text.Json;
using AgentSmith.Application.Services.Json;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0387: detects a raw LLM-provider error payload inside a run summary — a
/// JSON object with an error.message (the Anthropic shape
/// {"type":"error","error":{"message":...}}) — and extracts the inner message.
/// Pure transformation; anything that is not such a payload yields null.
/// </summary>
public static class ProviderErrorPayloadParser
{
    /// <summary>The payload's error.message, or null when the text carries no such payload.</summary>
    // 2026-10-26-cb25d: the payload wherever it sits in the summary; an object without error.message is passed over.
    public static string? TryExtractMessage(string text) => JsonAnswerReader.ReadObject(text, ErrorMessage);

    private static string? ErrorMessage(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error)
            || error.ValueKind != JsonValueKind.Object
            || !error.TryGetProperty("message", out var message)
            || message.ValueKind != JsonValueKind.String)
            return null;

        var value = message.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
