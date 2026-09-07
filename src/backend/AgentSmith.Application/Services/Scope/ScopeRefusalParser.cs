using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// Reads the REFUSAL out of a scope-classifier reply on its own, without the reply
/// having to be a usable repo verdict — the same independence
/// <see cref="ScopeEstimateParser"/> has, for the same reason: <see cref="RepoScopeParser"/>
/// refuses a reply without a <c>repos</c> array, and a refusing reply for a
/// single-repository run has no reason to carry one.
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
