using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// Reads the REFUSAL out of a scope-call reply on its own, beside the estimate (<see cref="ScopeEstimateParser"/>),
/// so a reply carrying one is never refused for lacking the other.
/// </summary>
public static class ScopeRefusalParser
{
    /// <summary>
    /// The first object in the reply that states a refusal; null when none does —
    /// an absent field means the ticket was not objected to, exactly as before the
    /// field existed.
    /// </summary>
    public static ScopeRefusal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var json in ReplyJsonObjects.In(text))
        {
            var refusal = ReadRefusal(json);
            if (refusal is not null) return refusal;
        }
        return null;
    }

    private static ScopeRefusal? ReadRefusal(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
        using (doc)
        {
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? RepoScopeReplyFields.ReadRefusal(doc.RootElement)
                : null;
        }
    }
}
