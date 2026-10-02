using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-02-3f06a: the list-shaped keys of a phase-spec map, read into what a
/// <see cref="PhaseDraft"/> carries. Pure transformation over the deserialized YAML.
/// <para>
/// The schema declares each shape, but a spec read back from a branch was never validated,
/// so every reader here drops what it cannot read rather than stringifying it — a
/// stringified map is a .NET type name, and that name reached the premise checker and
/// the ticket comment as if it were an assumption.
/// </para>
/// </summary>
public static class PhaseDraftLists
{
    /// <summary>A string or a list of strings, as requires, tests and contexts are written.</summary>
    public static IReadOnlyList<string> Strings(IReadOnlyDictionary<string, object?> map, string field)
    {
        if (!map.TryGetValue(field, out var value) || value is null) return [];
        return value switch
        {
            string single => [single],
            List<object?> list => [.. list.Select(e => e?.ToString() ?? string.Empty)],
            _ => [],
        };
    }

    /// <summary>
    /// Each assumption as its premise: a string as written, a <c>{claim, check}</c> map as
    /// its claim. The check says how to test the premise and is not itself one, so it is not
    /// read; a map without a string claim is dropped.
    /// </summary>
    public static IReadOnlyList<string> Assumptions(IReadOnlyDictionary<string, object?> map)
    {
        if (!map.TryGetValue("assumptions", out var value) || value is null) return [];
        return value switch
        {
            string single => [single],
            List<object?> list => [.. list.Select(Assumption).OfType<string>()],
            _ => [],
        };
    }

    public static IReadOnlyList<PhaseFact> Facts(IReadOnlyDictionary<string, object?> map)
    {
        if (!map.TryGetValue("facts", out var value) || value is not List<object?> list) return [];
        return [.. list
            .OfType<Dictionary<object, object?>>()
            .Select(f => new PhaseFact(
                GetString(f, "claim") ?? string.Empty, GetString(f, "evidence") ?? string.Empty))
            .Where(f => f.Claim.Length > 0)];
    }

    /// <summary>A non-blank string value of a nested map, or null.</summary>
    public static string? GetString(Dictionary<object, object?> entry, string key) =>
        entry.TryGetValue(key, out var value) && value is string text
        && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static string? Assumption(object? entry) => entry switch
    {
        string text => text,
        Dictionary<object, object?> premise => GetString(premise, "claim"),
        _ => null,
    };
}
