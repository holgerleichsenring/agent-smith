namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: facet + text matching for recall(query) — grep-first, no vector
/// index. A <c>type:feedback</c> token filters on the facet; every other
/// token must appear (case-insensitive) in the entry's name, description, or
/// body. <c>[[slug]]</c> citations match the entry name directly.
/// </summary>
public static class MemoryQueryMatcher
{
    private const string FacetPrefix = "type:";

    public static bool Matches(MemoryEntry entry, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;
        foreach (var token in Tokenize(query))
        {
            if (token.StartsWith(FacetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!MemoryEntryTypes.TryParse(token[FacetPrefix.Length..], out var type)
                    || entry.Type != type)
                    return false;
                continue;
            }
            if (!ContainsToken(entry, token)) return false;
        }
        return true;
    }

    private static IEnumerable<string> Tokenize(string query) =>
        query.Split([' ', '\t', '\n', '\r', ',', '[', ']'], StringSplitOptions.RemoveEmptyEntries);

    private static bool ContainsToken(MemoryEntry entry, string token) =>
        entry.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
        || entry.Description.Contains(token, StringComparison.OrdinalIgnoreCase)
        || entry.Body.Contains(token, StringComparison.OrdinalIgnoreCase);
}
