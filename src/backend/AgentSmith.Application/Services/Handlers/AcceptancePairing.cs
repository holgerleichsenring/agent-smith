using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-06-9f14: pairs the master's dispositions with the ratified criteria by the text
/// each one carries, falling back to position PER DISPOSITION.
/// <para>
/// The skill asks for the criterion verbatim and the deployed master writes it, so the name
/// is there to match on. A disposition whose name matches nothing keeps the slot its
/// position would have given it — an answer whose every name misses (the harness fixtures
/// answer "criterion 1") pairs exactly as it did before, and a non-matching name never
/// removes a pairing position would have made. Comparison is on trimmed text with collapsed
/// whitespace, trailing punctuation dropped and case ignored; nothing fuzzier. A done-list
/// may repeat a string, so duplicate texts pair in order of appearance.
/// </para>
/// </summary>
internal static class AcceptancePairing
{
    internal static IReadOnlyList<CriterionPairing> Pair(
        IReadOnlyList<string> criteria, IReadOnlyList<AcceptanceDisposition> dispositions)
    {
        var slots = new AcceptanceDisposition?[criteria.Count];
        var by = new PairedBy[criteria.Count];
        var unmatched = MatchByName(criteria, dispositions, slots, by);
        FallBackToPosition(dispositions, unmatched, slots, by);
        return criteria
            .Select((text, i) => new CriterionPairing(
                i, text, slots[i], slots[i] is null ? PairedBy.Nothing : by[i]))
            .ToList();
    }

    private static List<int> MatchByName(
        IReadOnlyList<string> criteria, IReadOnlyList<AcceptanceDisposition> dispositions,
        AcceptanceDisposition?[] slots, PairedBy[] by)
    {
        var keys = criteria.Select(Normalise).ToList();
        var unmatched = new List<int>();
        for (var i = 0; i < dispositions.Count; i++)
        {
            var slot = FreeSlotNamed(keys, slots, Normalise(dispositions[i].Criterion));
            if (slot < 0) { unmatched.Add(i); continue; }
            slots[slot] = dispositions[i];
            by[slot] = PairedBy.Name;
        }
        return unmatched;
    }

    private static void FallBackToPosition(
        IReadOnlyList<AcceptanceDisposition> dispositions, List<int> unmatched,
        AcceptanceDisposition?[] slots, PairedBy[] by)
    {
        foreach (var i in unmatched)
        {
            if (i >= slots.Length || slots[i] is not null) continue;
            slots[i] = dispositions[i];
            by[i] = PairedBy.Position;
        }
    }

    // The first still-free criterion carrying this text — which is what makes a repeated
    // criterion pair in order of appearance. An empty name is no name.
    private static int FreeSlotNamed(List<string> keys, AcceptanceDisposition?[] slots, string name)
    {
        if (name.Length == 0) return -1;
        for (var i = 0; i < keys.Count; i++)
            if (slots[i] is null && keys[i] == name) return i;
        return -1;
    }

    internal static string Normalise(string text)
    {
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var end = collapsed.Length;
        while (end > 0 && (char.IsPunctuation(collapsed[end - 1]) || char.IsWhiteSpace(collapsed[end - 1])))
            end--;
        return collapsed[..end].ToLowerInvariant();
    }
}
