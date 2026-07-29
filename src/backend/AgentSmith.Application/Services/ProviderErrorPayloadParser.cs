using System.Text.Json;

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
    public static string? TryExtractMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        return ParseErrorMessage(text[start..(end + 1)]);
    }

    private static string? ParseErrorMessage(string candidate)
    {
        try
        {
            using var doc = JsonDocument.Parse(candidate);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            if (!doc.RootElement.TryGetProperty("error", out var error)
                || error.ValueKind != JsonValueKind.Object
                || !error.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.String)
                return null;

            var value = message.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (JsonException)
        {
            // Not JSON at all — a prose summary that merely contains braces.
            return null;
        }
    }
}
