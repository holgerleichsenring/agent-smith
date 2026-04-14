namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Extracts the first JSON object from LLM response text that may contain
/// markdown or prose around the JSON.
/// </summary>
internal static class JsonExtractor
{
    internal static string Extract(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text[start..(end + 1)];
        return text;
    }
}
