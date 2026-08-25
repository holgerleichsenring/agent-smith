using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413a: reads the ticket ESTIMATE — complexity tier and work shape — out of a
/// scope-classifier reply on its own, without the reply having to be a usable
/// repo verdict. <see cref="RepoScopeParser"/> refuses a reply that carries no
/// <c>repos</c> array, which is the right contract for scoping and the wrong one
/// for estimating: a run with one repository has nothing to scope and the model
/// has no reason to list it, yet the size and shape it states are still the
/// facts that size the run's ceiling and its cut.
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
