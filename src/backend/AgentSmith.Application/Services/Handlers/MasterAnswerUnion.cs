using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-7df4: combines the observation arrays several passes of one scan produced
/// into a single array, keeping every finding that only one of them held.
/// <para>
/// The coverage re-drive used to assign the deeper pass's result outright, on the comment
/// that the deeper pass "re-emits the complete observation array". It cannot: it opens on
/// an empty transcript, so anything the first pass found and the second did not was simply
/// discarded. Whether the second pass should CONTINUE the first is a context-size question;
/// that the two passes' findings should be UNIONED is not a question at all.
/// </para>
/// <para>
/// The union works on the raw object literals, so every field survives verbatim — no
/// re-serialisation, and a literal a strict parse would reject is carried the way the
/// resilient recovery carries it. A later pass restating a finding replaces the earlier
/// wording in place; first-seen order is kept.
/// </para>
/// </summary>
public sealed class MasterAnswerUnion(ITolerantJsonParser tolerantParser)
{
    /// <summary>
    /// The union of every answer's observation literals, or null when no answer held one —
    /// the caller then still has the original text, and a text that is not findings at all
    /// must stay that way so the merge can degrade on it.
    /// </summary>
    public string? Combine(IReadOnlyList<string> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var answer in answers)
        {
            foreach (var literal in tolerantParser.ExtractArrayObjects(answer ?? string.Empty))
            {
                var key = KeyOf(literal);
                if (!byKey.ContainsKey(key)) order.Add(key);
                byKey[key] = literal;
            }
        }
        return order.Count == 0
            ? null
            : "[" + string.Join(",\n", order.Select(key => byKey[key])) + "]";
    }

    /// <summary>
    /// What makes two literals the same finding: the location and the wording. Severity is
    /// deliberately out — a pass that re-rates a finding it already reported is restating
    /// it, not reporting a second one.
    /// </summary>
    private string KeyOf(string literal)
    {
        try
        {
            using var document = JsonDocument.Parse(literal);
            var root = document.RootElement;
            var file = tolerantParser.GetStringOrNull(root, "file") ?? string.Empty;
            var description = tolerantParser.GetStringOrNull(root, "description") ?? string.Empty;
            return $"{file}|{StartLine(root)}|{Normalize(description)}";
        }
        catch (JsonException)
        {
            return literal;
        }
    }

    private static string StartLine(JsonElement root) =>
        root.TryGetProperty("start_line", out var line) && line.ValueKind == JsonValueKind.Number
            ? line.GetInt32().ToString(CultureInfo.InvariantCulture)
            : string.Empty;

    private static string Normalize(string text) =>
        string.Join(' ', text.ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
