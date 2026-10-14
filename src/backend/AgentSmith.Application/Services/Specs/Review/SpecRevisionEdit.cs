using AgentSmith.Contracts.Events;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762f: the word-level judgments of <see cref="SpecRevisionGuard"/> over parsed yaml values —
/// strings, mappings and lists as <see cref="SpecDialog.OutcomeYamlReader.ReadMap"/> returns them. Words are
/// <see cref="SpecQuotableText"/>'s, so folding, quoting and punctuation never decide.
/// </summary>
internal static class SpecRevisionEdit
{
    /// <summary>True when the two parsed values are the same value, key by key and item by item.</summary>
    public static bool Equal(object? left, object? right) => (left, right) switch
    {
        (null, null) => true,
        (string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
        (Dictionary<object, object?> a, Dictionary<object, object?> b) => a.Count == b.Count
            && a.All(entry => b.TryGetValue(entry.Key, out var other) && Equal(entry.Value, other)),
        (List<object?> a, List<object?> b) => a.Count == b.Count && a.Zip(b).All(pair => Equal(pair.First, pair.Second)),
        _ => false,
    };

    /// <summary>
    /// True when <paramref name="revised"/> is <paramref name="original"/> with cut quotes replaced and
    /// nothing else changed: every string is judged by <see cref="IsEdit"/>, every mapping keeps its keys,
    /// every list its length.
    /// </summary>
    public static bool IsItemEdit(object? original, object? revised, IReadOnlyList<SpecReviewCut> cuts) =>
        (original, revised) switch
        {
            (string a, string b) => IsEdit(a, b, cuts),
            (Dictionary<object, object?> a, Dictionary<object, object?> b) => a.Count == b.Count
                && a.All(entry => b.TryGetValue(entry.Key, out var other) && IsItemEdit(entry.Value, other, cuts)),
            (List<object?> a, List<object?> b) => a.Count == b.Count
                && a.Zip(b).All(pair => IsItemEdit(pair.First, pair.Second, cuts)),
            _ => Equal(original, revised),
        };

    /// <summary>
    /// True when the revised text's words are the original's, word for word, except where a run of a cut's
    /// quote was replaced by that cut's replacement — or dropped, for a cut with none. Each occurrence may
    /// be applied or left; nothing else may differ.
    /// </summary>
    public static bool IsEdit(string original, string revised, IReadOnlyList<SpecReviewCut> cuts)
    {
        var from = SpecQuotableText.Words(original);
        var to = SpecQuotableText.Words(revised);
        var edits = cuts
            .Select(cut => (Quote: SpecQuotableText.Words(cut.Quote), With: SpecQuotableText.Words(cut.Replacement)))
            .Where(edit => edit.Quote.Length > 0)
            .ToList();
        var reached = new bool[from.Length + 1, to.Length + 1];
        reached[0, 0] = true;
        for (var i = 0; i <= from.Length; i++)
            for (var j = 0; j <= to.Length; j++)
            {
                if (!reached[i, j]) continue;
                if (i < from.Length && j < to.Length && from[i] == to[j]) reached[i + 1, j + 1] = true;
                foreach (var (quote, with) in edits)
                    if (Starts(from, i, quote) && Starts(to, j, with)) reached[i + quote.Length, j + with.Length] = true;
            }
        return reached[from.Length, to.Length];
    }

    /// <summary>True when one of the item's texts holds the quote as a run of its words.</summary>
    public static bool Carries(object? item, string? quote) =>
        Texts(item).Any(text => SpecQuotableText.IsRunIn(text, quote));

    /// <summary>True when every text of the item is a run of the words of <paramref name="source"/>.</summary>
    public static bool StandsIn(object? item, string? source)
    {
        var words = SpecQuotableText.Words(source);
        var texts = Texts(item).Select(SpecQuotableText.Words).Where(w => w.Length > 0).ToList();
        return texts.Count > 0 && texts.All(text => SpecQuotableText.ContainsRun(words, text));
    }

    /// <summary>True when <paramref name="run"/>'s words, at least one, stand in a row in the text.</summary>
    public static bool HoldsRun(string? text, string? run)
    {
        var words = SpecQuotableText.Words(run);
        return words.Length > 0 && SpecQuotableText.ContainsRun(SpecQuotableText.Words(text), words);
    }

    /// <summary>Every string the item holds, at any depth.</summary>
    public static IEnumerable<string> Texts(object? item) => item switch
    {
        string text => [text],
        Dictionary<object, object?> map => map.Values.SelectMany(Texts),
        List<object?> list => list.SelectMany(Texts),
        _ => [],
    };

    private static bool Starts(string[] words, int at, string[] run) =>
        at + run.Length <= words.Length && words.AsSpan(at, run.Length).SequenceEqual(run);
}
