using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: extracts the ScopeRepos classifier's JSON verdict from the model
/// reply. Tolerant like MasterVerificationParser — accepts a fenced block or a
/// bare JSON object anywhere in the text (first balanced object wins; the
/// classifier is a single-shot call, not a conversation). Returns null when no
/// object with a recognisable "repos" array is present — the handler treats
/// that as a parse failure and keeps all repos.
/// <para>
/// p0413: the reply's OPTIONAL fields (contexts, expected changes, complexity
/// tier, work shape) are read by <see cref="RepoScopeReplyFields"/>; this owns
/// the reply's shape and the per-repo verdicts.
/// </para>
/// </summary>
public static class RepoScopeParser
{
    public static RepoScopeClassification? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var json in ReplyJsonObjects.In(text))
            if (TryReadObject(json, out var classification))
                return classification;
        return null;
    }

    private static bool TryReadObject(string json, out RepoScopeClassification classification)
    {
        classification = null!;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return false; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!RepoScopeJson.TryGet(doc.RootElement, "repos", out var reposEl)
                || reposEl.ValueKind != JsonValueKind.Array)
                return false;
            var repos = reposEl.EnumerateArray()
                .Select(ReadVerdict)
                .Where(v => v is not null)
                .Select(v => v!)
                .ToList();
            classification = new RepoScopeClassification(
                repos, RepoScopeReplyFields.ReadRationale(doc.RootElement),
                RepoScopeReplyFields.ReadContexts(doc.RootElement),
                RepoScopeReplyFields.ReadTier(doc.RootElement),
                RepoScopeReplyFields.ReadExpectedChanges(doc.RootElement),
                RepoScopeReplyFields.ReadShape(doc.RootElement));
            return true;
        }
    }

    // p0386: an object entry {"name", "affected", "confidence", "reason"} is the
    // contract; a bare string is tolerated as affected=true (LLM output fuzz at
    // the parse boundary — an affected repo is always kept, so tolerance stays
    // fail-open). Absent/malformed affected reads as true for the same reason.
    private static RepoScopeVerdict? ReadVerdict(JsonElement entry)
    {
        if (entry.ValueKind == JsonValueKind.String)
        {
            var bare = entry.GetString()!.Trim();
            return bare.Length == 0 ? null : new RepoScopeVerdict(bare, Affected: true, Confidence: 0);
        }
        if (entry.ValueKind != JsonValueKind.Object) return null;
        var name = RepoScopeJson.ReadString(entry, "name")?.Trim() ?? string.Empty;
        if (name.Length == 0) return null;
        var affected = !RepoScopeJson.TryGet(entry, "affected", out var affectedEl)
            || affectedEl.ValueKind != JsonValueKind.False;
        return new RepoScopeVerdict(
            name, affected, ReadConfidence(entry), RepoScopeJson.ReadString(entry, "reason"));
    }

    // Absent / unreadable confidence reads as 0.0 — conservative: a shrugged
    // exclusion never clears the evaluator's floor, so the repo stays kept.
    private static double ReadConfidence(JsonElement obj)
    {
        if (!RepoScopeJson.TryGet(obj, "confidence", out var el)) return 0;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String
            && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var s))
            return s;
        return 0;
    }
}
