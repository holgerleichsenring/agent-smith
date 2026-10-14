using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: one list register of a revision judged item by item. Old and new items are paired by a
/// longest common subsequence over whole-item equality — steps by id — so one removed middle item leaves the
/// items after it paired, not changed. Within a gap between pairs, old and new are paired in order as
/// changed; the surplus is removed or added.
/// </summary>
internal sealed class SpecRevisionLists(
    IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> problems, bool lastRound)
{
    /// <summary>The claims of facts removed on the last round for an evidence problem — each may come back
    /// as an assumption.</summary>
    public List<string> MovedClaims { get; } = [];

    /// <summary>Null when every pair, removal and addition of the register is one the cuts explain.</summary>
    public string? Judge(string register, List<object?> original, List<object?> revised)
    {
        Func<object?, object?, bool> same = register == SpecRegisterNames.Steps ? SameId : SpecRevisionEdit.Equal;
        var (o, n) = (0, 0);
        foreach (var (i, j) in Pairs(original, revised, same).Append((original.Count, revised.Count)))
        {
            var gap = Math.Min(i - o, j - n);
            for (var k = 0; k < gap; k++)
                if (!SpecRevisionEdit.IsItemEdit(original[o + k], revised[n + k], cuts))
                    return $"a {register} item was changed beyond its cuts";
            for (var k = o + gap; k < i; k++)
                if (!Removable(register, original[k])) return $"a {register} item no cut quotes was removed";
            for (var k = n + gap; k < j; k++)
                if (!Addable(register, revised[k])) return $"a {register} item no cut's replacement holds was added";
            if (i < original.Count && !SpecRevisionEdit.IsItemEdit(original[i], revised[j], cuts))
                return $"a {register} item was changed beyond its cuts";
            (o, n) = (i + 1, j + 1);
        }
        return null;
    }

    /// <summary>Null when every old assumption stands and each added one is the exact claim of a fact moved
    /// on the last round.</summary>
    public string? JudgeAssumptions(List<object?> original, List<object?> revised)
    {
        var pairs = Pairs(original, revised, SpecRevisionEdit.Equal);
        if (pairs.Count != original.Count) return "an assumption was changed or removed";
        var kept = pairs.Select(pair => pair.New).ToHashSet();
        var added = revised.Where((_, index) => !kept.Contains(index));
        return added.All(item => SpecRevisionEdit.Texts(Field(item, "claim")).Take(1)
                .Any(claim => SpecQuotableText.IsVerbatim(MovedClaims, claim)))
            ? null
            : "an assumption was added that is no fact moved on the last round";
    }

    private bool Removable(string register, object? item)
    {
        if (cuts.Any(cut => SpecRevisionEdit.Carries(item, cut.Quote))) return true;
        if (!lastRound || register != SpecRegisterNames.Facts) return false;
        var claim = SpecRevisionEdit.Texts(Field(item, "claim")).FirstOrDefault();
        if (claim is null || !problems.Any(p => SpecQuotableText.IsVerbatim([p.Claim], claim))) return false;
        MovedClaims.Add(claim);
        return true;
    }

    private bool Addable(string register, object? item) =>
        cuts.Any(cut => cut.Register == register && cut.Replacement is not null
            && SpecRevisionEdit.StandsIn(item, cut.Replacement));

    private static bool SameId(object? left, object? right) =>
        SpecRevisionEdit.Equal(left, right)
        || (Field(left, "id") is string a && Field(right, "id") is string b && string.Equals(a, b, StringComparison.Ordinal));

    private static object? Field(object? item, string key) =>
        item is Dictionary<object, object?> map ? map.GetValueOrDefault(key) : item;

    // Longest common subsequence, as (old index, new index) pairs in order.
    private static List<(int Old, int New)> Pairs(
        List<object?> original, List<object?> revised, Func<object?, object?, bool> same)
    {
        var length = new int[original.Count + 1, revised.Count + 1];
        for (var i = original.Count - 1; i >= 0; i--)
            for (var j = revised.Count - 1; j >= 0; j--)
                length[i, j] = same(original[i], revised[j])
                    ? length[i + 1, j + 1] + 1
                    : Math.Max(length[i + 1, j], length[i, j + 1]);
        var pairs = new List<(int, int)>();
        for (int i = 0, j = 0; i < original.Count && j < revised.Count;)
        {
            if (same(original[i], revised[j])) pairs.Add((i++, j++));
            else if (length[i + 1, j] >= length[i, j + 1]) i++;
            else j++;
        }
        return pairs;
    }
}
