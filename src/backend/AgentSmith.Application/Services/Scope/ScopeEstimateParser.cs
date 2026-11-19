using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413a: reads the ticket ESTIMATE — complexity tier and work shape — out of a scope-call reply, the first
/// object that states either. 2026-11-19-6b0bb: the reply carries no repo verdict any more.
/// </summary>
public static class ScopeEstimateParser
{
    /// <summary>
    /// The first object in the reply that states a tier or a shape.
    /// <see cref="ScopeEstimate.None"/> when the reply states neither — the same
    /// fail-safe every absent field in this reply has: nothing recorded, and
    /// every consumer behaves exactly as it did before.
    /// </summary>
    public static ScopeEstimate Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ScopeEstimate.None;
        foreach (var json in ReplyJsonObjects.In(text))
        {
            var estimate = ReadEstimate(json);
            if (estimate.IsStated) return estimate;
        }
        return ScopeEstimate.None;
    }

    private static ScopeEstimate ReadEstimate(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return ScopeEstimate.None; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return ScopeEstimate.None;
            return new ScopeEstimate(
                RepoScopeReplyFields.ReadTier(doc.RootElement),
                RepoScopeReplyFields.ReadShape(doc.RootElement));
        }
    }
}
