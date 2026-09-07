using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Workers;

/// <summary>
/// p0419: the envelope-shaped questions about free-form worker output. Where an object
/// ENDS is <see cref="Contracts.Json.JsonObjectSpans"/>'s business, shared with every
/// other parser of model output; what an object MEANS is the reply parser's. This
/// class holds the two questions in between.
/// </summary>
internal static class JsonObjectScanner
{
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
