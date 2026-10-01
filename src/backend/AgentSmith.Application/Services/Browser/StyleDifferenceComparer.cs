namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: the EXACT level of a comparison. Two computed strings are equal or they are
/// not, so every difference has a name and both values; nothing is weighted, rounded or skipped.
/// A selector that matches nothing on either side is a named difference, never a silent skip.
/// Pure: the browser computed the values, this only lists where they part.
/// </summary>
public sealed class StyleDifferenceComparer
{
    /// <summary>Every difference, in pair order and then property order.</summary>
    public IReadOnlyList<StyleDifference> Compare(
        IReadOnlyList<SelectorPair> pairs, IReadOnlyList<BrowserStyleRow> reference,
        IReadOnlyList<BrowserStyleRow> candidate, IReadOnlyList<string> properties)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        ArgumentNullException.ThrowIfNull(properties);
        var differences = new List<StyleDifference>();
        for (var i = 0; i < pairs.Count; i++)
            differences.AddRange(Differences(pairs[i], reference.ElementAtOrDefault(i), candidate.ElementAtOrDefault(i), properties));
        return differences;
    }

    private static IEnumerable<StyleDifference> Differences(
        SelectorPair pair, BrowserStyleRow? reference, BrowserStyleRow? candidate, IReadOnlyList<string> properties)
    {
        var (left, right) = (Matched(reference), Matched(candidate));
        if (left is null || right is null)
        {
            yield return new StyleDifference(pair.Reference, pair.Candidate, StyleDifference.MatchProperty,
                Describe(reference), Describe(candidate));
            yield break;
        }
        foreach (var property in properties)
        {
            var (a, b) = (left.GetValueOrDefault(property) ?? string.Empty, right.GetValueOrDefault(property) ?? string.Empty);
            if (!string.Equals(a, b, StringComparison.Ordinal))
                yield return new StyleDifference(pair.Reference, pair.Candidate, property, a, b);
        }
    }

    private static IReadOnlyDictionary<string, string>? Matched(BrowserStyleRow? row) =>
        row is { Count: > 0, Values: { } values } ? values : null;

    private static string Describe(BrowserStyleRow? row) =>
        row is { Count: > 0 } ? $"{row.Count} element{(row.Count == 1 ? "" : "s")}" : StyleDifference.NoMatch;
}
