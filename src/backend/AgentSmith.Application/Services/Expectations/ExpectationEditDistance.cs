namespace AgentSmith.Application.Services.Expectations;

/// <summary>
/// p0328: Levenshtein distance between the drafted and the ratified canonical
/// renderings — the raw material for the p0329 first-PR-acceptance metric.
/// Inputs are capped so a pathological edit cannot allocate an unbounded
/// matrix; the metric loses nothing at that scale.
/// </summary>
internal static class ExpectationEditDistance
{
    private const int MaxLength = 8_000;

    public static int Between(string original, string edited)
    {
        var a = Cap(original);
        var b = Cap(edited);
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    private static string Cap(string text) =>
        text.Length <= MaxLength ? text : text[..MaxLength];
}
