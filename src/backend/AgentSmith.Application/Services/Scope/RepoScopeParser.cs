using System.Text.Json;

namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0331: extracts the ScopeRepos classifier's JSON verdict from the model
/// reply. Tolerant like MasterVerificationParser — accepts a fenced block or a
/// bare JSON object anywhere in the text (first balanced object wins; the
/// classifier is a single-shot call, not a conversation). Returns null when no
/// object with a recognisable "repos" array is present — the handler treats
/// that as a parse failure and keeps all repos.
/// </summary>
public static class RepoScopeParser
{
    public static RepoScopeClassification? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var json in BalancedObjects(text))
            if (TryReadObject(json, out var classification))
                return classification;
        return null;
    }

    private static IEnumerable<string> BalancedObjects(string text)
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

    private static bool TryReadObject(string json, out RepoScopeClassification classification)
    {
        classification = null!;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return false; }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!TryGet(doc.RootElement, "repos", out var reposEl)
                || reposEl.ValueKind != JsonValueKind.Array)
                return false;
            var repos = reposEl.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            classification = new RepoScopeClassification(
                repos, ReadConfidence(doc.RootElement), ReadRationale(doc.RootElement));
            return true;
        }
    }

    // Absent / unreadable confidence reads as 0.0 — conservative: the handler's
    // confidence floor then keeps all repos rather than trusting a shrug.
    private static double ReadConfidence(JsonElement obj)
    {
        if (!TryGet(obj, "confidence", out var el)) return 0;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var n)) return n;
        if (el.ValueKind == JsonValueKind.String
            && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var s))
            return s;
        return 0;
    }

    private static string? ReadRationale(JsonElement obj) =>
        TryGet(obj, "rationale", out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        value = default;
        return false;
    }
}
