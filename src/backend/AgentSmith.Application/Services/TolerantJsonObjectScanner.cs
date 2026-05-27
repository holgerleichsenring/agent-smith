namespace AgentSmith.Application.Services;

/// <summary>
/// Brace-counting state-machine that yields complete top-level `{...}` object
/// literals from a possibly-truncated JSON-array response. String-literal
/// aware (unescaped `}` inside a string does not close the object). Half-
/// finished trailing objects after truncation are dropped silently — callers
/// decide how to handle the zero-result yield.
/// Used by <see cref="TolerantJsonParser"/> as the resilient fallback when
/// strict JsonDocument.Parse throws.
/// </summary>
internal static class TolerantJsonObjectScanner
{
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
