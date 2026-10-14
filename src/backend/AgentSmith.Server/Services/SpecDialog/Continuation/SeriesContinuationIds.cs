using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: a worked ticket's series continued BY SPEC ID. A draft whose id ran is kept and a
/// changed text of it is reported as discarded; an unstarted id the proposal repeats is re-cut (kept only
/// when its yaml is byte-identical); an unstarted id a lone spec leaves out is kept, one an epic leaves
/// out is dropped; an unknown id is appended under the next member id no basis, executed or recorded
/// spec holds, and every requires edge follows it.
/// </summary>
public static class SeriesContinuationIds
{
    public static TicketContinuationDecision Continue(
        TicketContinuationState state, IReadOnlyList<SpecDraft> drafts, bool epic, SeriesSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var executed = state.ExecutedIds.ToHashSet(StringComparer.Ordinal);
        var basis = state.Basis.GroupBy(b => b.SpecId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var taken = new HashSet<string>(basis.Keys.Concat(executed), StringComparer.Ordinal);
        taken.UnionWith(state.Record?.Set.Specs.Select(s => s.SpecId) ?? []);
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var specs = new List<TicketContinuationSpec>();
        var pastZ = false;
        foreach (var draft in drafts.Where(d => !ids.ContainsKey(d.SpecId)))
        {
            ids[draft.SpecId] = draft.SpecId;
            if (executed.Contains(draft.SpecId))
                specs.Add(new(draft.SpecId, draft.Goal, SpecDispositionKinds.Kept, Changed(basis, draft)
                    ? "it already ran, so the changed text is discarded" : "it already ran"));
            else if (basis.TryGetValue(draft.SpecId, out var unstarted))
                specs.Add(new(draft.SpecId, draft.Goal,
                    unstarted.Yaml == draft.Yaml ? SpecDispositionKinds.Kept : SpecDispositionKinds.Recut));
            else
            {
                var (next, index) = SeriesIdFactory.NextFree(state.Series ?? string.Empty, 0, taken);
                pastZ |= index >= SeriesConfig.MemberLetters; // 2026-10-13-8762u: dropped ids stay taken
                taken.Add(next);
                ids[draft.SpecId] = next;
                specs.Add(new(next, draft.Goal, SpecDispositionKinds.Appended));
            }
        }
        foreach (var left in state.Basis.Where(b => !ids.ContainsKey(b.SpecId)).DistinctBy(b => b.SpecId))
            specs.Add(executed.Contains(left.SpecId) ? new(left.SpecId, left.Goal, SpecDispositionKinds.Kept, "it already ran")
                : new(left.SpecId, left.Goal, epic ? SpecDispositionKinds.Dropped : SpecDispositionKinds.Kept,
                    epic ? "the approved cut leaves it out" : "a lone spec leaves the rest of the series as it is"));

        var unstartedCount = specs.Count(s => s.Disposition != SpecDispositionKinds.Dropped && !executed.Contains(s.SpecId));
        var recorded = Reid(drafts.Where(d => !executed.Contains(d.SpecId)).DistinctBy(d => d.SpecId), ids);
        // 2026-10-13-8762u: the series cap and the member letters, read live; an invalid block refuses naming its field.
        var over = pastZ ? $"has used its {SeriesConfig.MemberLetters} member letters (a..z); an appended spec would need one more"
            : executed.Count + unstartedCount > settings.Series.MaxSpecs ? $"would hold {executed.Count} executed and {unstartedCount} "
                + $"unstarted spec(s), more than series.max_specs ({settings.Series.MaxSpecs})" : null;
        var (effect, reason) = settings.Refusal() is { } invalid ? (TicketContinuationEffects.Refused, $"{invalid}. Nothing was recorded.")
            : over is not null ? (TicketContinuationEffects.RefusedOverCap, $"The series of ticket {state.TicketId} {over}. Nothing was recorded; propose fewer specs.")
            : (TicketContinuationEffects.Continue, $"Ticket {state.TicketId}'s next attempt works its series continued by this approval: "
                + $"{executed.Count} spec(s) already ran and stay as they ran, {unstartedCount} are still to run.");
        return new(effect, reason,
            executed.Count, unstartedCount, state.BasisRunId, specs, recorded) { Ids = ids };
    }

    private static bool Changed(IReadOnlyDictionary<string, BasisSpec> basis, SpecDraft draft) =>
        basis.TryGetValue(draft.SpecId, out var ran) && ran.Yaml != draft.Yaml;

    private static IReadOnlyList<SpecDraft> Reid(IEnumerable<SpecDraft> drafts, IReadOnlyDictionary<string, string> ids)
    {
        var rewriter = new SpecDraftIdRewriter();
        var moved = ids.Where(p => p.Key != p.Value).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return [.. drafts.Select(d => d with
        {
            SpecId = ids[d.SpecId],
            Requires = [.. d.Requires.Select(r => rewriter.RewriteValue(r, moved))],
            Yaml = rewriter.Rewrite(d.Yaml, moved),
        })];
    }
}
