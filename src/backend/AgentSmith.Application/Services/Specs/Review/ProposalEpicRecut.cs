using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfb: an epic's children merged from a re-proposal, licensed by REFUSE without cuts. A child this
/// round refused without cuts may be rewritten freely — its requires, repos and applies_to included — or dropped;
/// while at least one applying child was, the reply may add ids. Every other child keeps its id: a change beyond
/// its licence is reverted alone, a child missing from the reply is restored in its last valid text, and an added
/// id with no refusal behind it is left out; each is listed as reverted. Order is the reply's.
/// </summary>
internal static class ProposalEpicRecut
{
    public static ProposalChildMerge Merge(
        IReadOnlyList<SpecDraft> before, IReadOnlyList<SpecDraft> after,
        IReadOnlyDictionary<string, ProposalDraftLicence> licences, Func<SpecDraft, SpecDraft, bool> admits)
    {
        var recut = licences.Where(l => l.Value is { Refused: true, Cuts.Count: 0 })
            .Select(l => l.Key).ToHashSet(StringComparer.Ordinal);
        var old = before.ToDictionary(d => d.SpecId, StringComparer.Ordinal);
        var (kept, reverted, added) = (new List<SpecDraft>(), new List<string>(), new List<string>());
        foreach (var draft in after)
        {
            if (old.TryGetValue(draft.SpecId, out var was))
            {
                var admitted = Same(was, draft) || recut.Contains(draft.SpecId) || admits(was, draft);
                kept.Add(admitted ? draft : was);
                if (!admitted) reverted.Add(draft.SpecId);
            }
            else
            {
                (recut.Count > 0 ? added : reverted).Add(draft.SpecId);
                if (recut.Count > 0) kept.Add(draft);
            }
        }
        var stated = after.Select(d => d.SpecId).ToHashSet(StringComparer.Ordinal);
        var dropped = new List<string>();
        for (var i = 0; i < before.Count; i++)
        {
            if (stated.Contains(before[i].SpecId)) continue;
            if (recut.Contains(before[i].SpecId)) dropped.Add(before[i].SpecId);
            else
            {
                kept.Insert(Math.Min(i, kept.Count), before[i]);
                reverted.Add(before[i].SpecId);
            }
        }
        return new ProposalChildMerge(kept, reverted, added, dropped);
    }

    public static bool Same(SpecDraft a, SpecDraft b) => string.Equals(a.Yaml, b.Yaml, StringComparison.Ordinal);
}
