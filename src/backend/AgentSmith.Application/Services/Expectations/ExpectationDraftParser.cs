using System.Text.Json;
using AgentSmith.Contracts.Expectations;

namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: extracts the drafting model's JSON verdict from the reply. Tolerant
/// like RepoScopeParser — accepts a fenced block or a bare JSON object anywhere
/// in the text (first object with a recognisable "expected" array wins).
/// Returns null when no such object is present; the drafter treats that as a
/// retryable failure.
/// </summary>
public static class ExpectationDraftParser
{
    public static ExpectationDraft? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (var json in BalancedObjects(text))
            if (TryReadObject(json, out var draft))
                return draft;
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

    private static bool TryReadObject(string json, out ExpectationDraft draft)
    {
        draft = null!;
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException) { return false; }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!TryGet(root, "expected", out var expectedEl)
                || expectedEl.ValueKind != JsonValueKind.Array)
                return false;
            draft = new ExpectationDraft(
                ReadString(root, "observed"),
                ReadStrings(expectedEl),
                TryGet(root, "constraints", out var c) && c.ValueKind == JsonValueKind.Array
                    ? ReadStrings(c) : [],
                ReadOpenQuestion(root));
            return true;
        }
    }

    private static ExpectationOpenQuestion? ReadOpenQuestion(JsonElement root)
    {
        if (!TryGet(root, "open_question", out var el) || el.ValueKind != JsonValueKind.Object)
            return null;
        return new ExpectationOpenQuestion(
            ReadString(el, "question"), ReadString(el, "option_a"), ReadString(el, "option_b"));
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement array) =>
        array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!.Trim())
            .Where(s => s.Length > 0)
            .ToList();

    private static string ReadString(JsonElement obj, string name) =>
        TryGet(obj, name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()!.Trim() : string.Empty;

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
