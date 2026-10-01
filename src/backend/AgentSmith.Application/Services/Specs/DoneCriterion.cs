using AgentSmith.Application.Services.SpecDialog;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-01-f5c3a: one entry of a spec's done list, as the LINE every reader of it sees. An
/// entry is a line or a scenario {given?, when, then}; a scenario reads as
/// "GIVEN … WHEN … THEN …" in plain keywords — no markup, because the ticket, the prompt and the
/// read-back of a filed body must all equal the same string, and markup survives one tracker
/// encoding and not the next. Flattened here, where the yaml is read, so every consumer of
/// <c>PhaseDraft.Done</c> keeps taking a string.
/// </summary>
public static class DoneCriterion
{
    /// <summary>The line form of one done entry; empty when the entry says nothing.</summary>
    public static string Line(object? entry) => entry switch
    {
        null => string.Empty,
        string line => line.Trim(),
        Dictionary<object, object?> scenario => Scenario(scenario),
        _ => entry.ToString()?.Trim() ?? string.Empty,
    };

    /// <summary>The line form of every entry of the map's done list, in order.</summary>
    public static IReadOnlyList<string> Lines(IReadOnlyDictionary<string, object?> map) =>
        map.TryGetValue("done", out var value) ? value switch
        {
            string single => [Line(single)],
            List<object?> list => [.. list.Select(Line)],
            _ => [],
        } : [];

    private static string Scenario(Dictionary<object, object?> scenario)
    {
        var parts = new[] { "given", "when", "then" }
            .Select(key => (Key: key, Text: Part(scenario, key)))
            .Where(part => part.Text.Length > 0)
            .Select(part => $"{part.Key.ToUpperInvariant()} {part.Text}");
        return string.Join(" ", parts);
    }

    private static string Part(Dictionary<object, object?> scenario, string key) =>
        scenario.TryGetValue(key, out var value) && value is not null
            ? CriterionLine.Collapse(value.ToString() ?? string.Empty)
            : string.Empty;
}
