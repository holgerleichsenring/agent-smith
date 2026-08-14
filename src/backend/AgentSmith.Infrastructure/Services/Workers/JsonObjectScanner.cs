using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0419: finds JSON objects inside free-form agent output. Text-shape work only —
/// what an object MEANS is the reply parser's business, and keeping the two apart is
/// what lets the parser read as the rule it encodes.
/// </summary>
internal static class JsonObjectScanner
{
    /// <summary>
    /// Every balanced {...} span, outermost first, so a nested payload never masks
    /// the envelope that contains it.
    /// </summary>
    public static IEnumerable<string> BalancedObjects(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            var depth = 0;
            for (var j = i; j < text.Length; j++)
            {
                if (text[j] == '{') depth++;
                else if (text[j] == '}' && --depth == 0)
                {
                    yield return text[i..(j + 1)];
                    i = j;
                    break;
                }
            }
        }
    }

    /// <summary>Strips one surrounding markdown fence, if the output wears one.</summary>
    public static string Unfence(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var firstBreak = text.IndexOf('\n');
        if (firstBreak < 0) return text;
        var body = text[(firstBreak + 1)..];
        var closing = body.LastIndexOf("```", StringComparison.Ordinal);
        return (closing < 0 ? body : body[..closing]).Trim();
    }

    /// <summary>
    /// Does this object carry a field the worker envelope actually defines?
    /// <para>
    /// Not pedantry: a structured-output call answers WITH a JSON object of its own
    /// ({"primary_language": …}), and deserialising that into a WorkerReply silently
    /// drops every field and yields an empty envelope. Run 6bad died twice that way.
    /// Two JSON contracts sit on top of each other here; only the named keys tell
    /// them apart.
    /// </para>
    /// </summary>
    public static bool HasEnvelopeField(string candidate)
    {
        try
        {
            using var doc = JsonDocument.Parse(candidate);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            foreach (var property in doc.RootElement.EnumerateObject())
                if (property.NameEquals("text") || property.NameEquals("tool_calls")
                    || property.NameEquals("toolCalls") || property.NameEquals("error"))
                    return true;
            return false;
        }
        catch (JsonException) { return false; }
    }
}
