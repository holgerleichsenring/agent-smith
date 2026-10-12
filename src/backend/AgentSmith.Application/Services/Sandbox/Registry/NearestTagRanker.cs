namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: the existing tags closest to one a registry does not have. A tag is cut
/// into its letter-and-digit runs ("eclipse-temurin-25_1.11.7" → eclipse, temurin, 25, 1, 11,
/// 7); a candidate ranks by how many of the requested runs it shares, then by the length of
/// the prefix it shares, then by how close its length is. No ecosystem's tag shape is known
/// here — the runs are the only reading, so any registry's tags rank the same way.
/// </summary>
public sealed class NearestTagRanker
{
    public const int MaxCandidates = 8;

    public IReadOnlyList<string> Nearest(string requested, IEnumerable<string> tags)
    {
        var wanted = Runs(requested);
        return tags
            .Where(tag => !string.Equals(tag, requested, StringComparison.Ordinal))
            .Select(tag => (Tag: tag, Shared: Shared(wanted, Runs(tag)), Prefix: CommonPrefix(requested, tag)))
            .Where(scored => scored.Shared > 0 || scored.Prefix > 0)
            .OrderByDescending(scored => scored.Shared)
            .ThenByDescending(scored => scored.Prefix)
            .ThenBy(scored => Math.Abs(scored.Tag.Length - requested.Length))
            .ThenByDescending(scored => scored.Tag, StringComparer.Ordinal)
            .Take(MaxCandidates)
            .Select(scored => scored.Tag)
            .ToList();
    }

    private static List<string> Runs(string tag)
    {
        var runs = new List<string>();
        var start = -1;
        for (var i = 0; i <= tag.Length; i++)
        {
            var inRun = i < tag.Length && char.IsLetterOrDigit(tag[i]);
            if (inRun && start < 0) start = i;
            if (inRun || start < 0) continue;
            runs.Add(tag[start..i].ToLowerInvariant());
            start = -1;
        }
        return runs;
    }

    // Counted as a multiset: "13" requested once is matched by one "13", however many there are.
    private static int Shared(IEnumerable<string> wanted, List<string> candidate)
    {
        var left = new List<string>(candidate);
        return wanted.Count(run => left.Remove(run));
    }

    private static int CommonPrefix(string a, string b)
    {
        var length = 0;
        while (length < a.Length && length < b.Length
               && char.ToLowerInvariant(a[length]) == char.ToLowerInvariant(b[length]))
            length++;
        return length;
    }
}
