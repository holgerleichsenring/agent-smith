namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Extracts complete top-level JSON object literals from a possibly-truncated
/// JSON-array response. Brace-counting state machine with string-literal awareness.
/// Doesn't validate object content (caller does that via JsonSerializer.Deserialize);
/// just finds boundaries.
///
/// Use case: filter LLM hits max_output_tokens mid-array → response ends in a
/// half-finished object. Strict JsonDocument.Parse throws; this extractor salvages
/// the complete objects before the truncation point.
/// </summary>
internal static class ResilientJsonObjectExtractor
{
    /// <summary>
    /// Walks the response, yields each complete `{...}` object literal found at
    /// outer-array depth. Half-finished trailing objects are silently skipped —
    /// the caller decides how to handle a zero-result yield.
    /// </summary>
    internal static IEnumerable<string> ExtractObjects(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText)) yield break;

        var inString = false;
        var escapeNext = false;
        var braceDepth = 0;
        var objectStart = -1;

        for (var i = 0; i < responseText.Length; i++)
        {
            var c = responseText[i];

            if (escapeNext) { escapeNext = false; continue; }
            if (inString)
            {
                if (c == '\\') { escapeNext = true; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }

            if (c == '{')
            {
                if (braceDepth == 0) objectStart = i;
                braceDepth++;
                continue;
            }
            if (c == '}')
            {
                braceDepth--;
                if (braceDepth == 0 && objectStart >= 0)
                {
                    yield return responseText[objectStart..(i + 1)];
                    objectStart = -1;
                }
            }
        }
    }
}
