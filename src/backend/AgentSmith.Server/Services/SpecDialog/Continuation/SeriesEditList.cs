using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// The full spec list an edit asks for: its edits applied in order to the series as the next attempt will read it.
/// Every yaml is validated against the spec schema before it is read, a recut keeps its spec's id, and an append
/// may not reuse an id the list holds — its id is minted anew when the edit is decided. The first edit that cannot
/// apply refuses the whole edit.
/// 2026-11-27-3532d: an appended spec whose <c>supersedes</c> names specs of the list retires them: they stay as they
/// are, the replacement stands right behind the last of them, and every later spec that required one is re-cut to
/// require the replacement. A spec that ran is never retired.
/// </summary>
public sealed class SeriesEditList(ISpecDraftValidator validator, SpecDraftReader reader, SpecDraftIdRewriter rewriter)
{
    /// <param name="ran">The ids of the specs that already ran.</param>
    public (IReadOnlyList<SpecDraft> Drafts, string? Error) Apply(
        IReadOnlyList<SpecDraft> series, IReadOnlyList<SeriesEdit> edits, IReadOnlyCollection<string>? ran = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0) return ([], "the edit names no change");
        var list = series.ToList();
        var held = series.ToList(); // every spec the list held at any edit
        for (var i = 0; i < edits.Count; i++)
        {
            var edit = edits[i];
            var error = edit.Kind switch
            {
                SeriesEditKinds.Recut => Recut(list, edit),
                SeriesEditKinds.Drop => Drop(list, edit),
                SeriesEditKinds.Append => Append(list, edit, ran ?? []),
                _ => $"kind '{edit.Kind}' is none of {SeriesEditKinds.Recut}, {SeriesEditKinds.Drop}, {SeriesEditKinds.Append}",
            };
            if (error is not null) return ([], $"edit {i + 1} ({edit.Kind} {edit.SpecId}): {error}");
            held.AddRange(list.Where(d => !held.Any(h => h.SpecId == d.SpecId)));
        }
        return Reopened(held, list) is { } reopened ? ([], reopened) : (list, null);
    }

    // 2026-11-27-3532d: a replacement dropped while the spec it retired stays would reopen that spec.
    private static string? Reopened(IReadOnlyList<SpecDraft> series, List<SpecDraft> list) =>
        series.Where(s => !list.Any(d => d.SpecId == s.SpecId))
            .SelectMany(s => s.Supersedes.Where(r => list.Any(d => d.SpecId == r)).Select(r => (Dropped: s.SpecId, Retired: r)))
            .Select(p => $"dropping {p.Dropped} would reopen {p.Retired}, which it replaces; drop {p.Retired} with it")
            .FirstOrDefault();

    private string? Recut(List<SpecDraft> list, SeriesEdit edit)
    {
        var index = list.FindIndex(d => d.SpecId == edit.SpecId);
        if (index < 0) return $"the series holds no spec '{edit.SpecId}'";
        var (draft, error) = Read(edit.Yaml);
        if (draft is null) return error;
        if (draft.SpecId != edit.SpecId)
            return $"the yaml's spec field is '{draft.SpecId}', not '{edit.SpecId}' — a recut keeps the spec's id";
        list[index] = draft;
        return null;
    }

    private static string? Drop(List<SpecDraft> list, SeriesEdit edit) =>
        list.RemoveAll(d => d.SpecId == edit.SpecId) == 0 ? $"the series holds no spec '{edit.SpecId}'" : null;

    private string? Append(List<SpecDraft> list, SeriesEdit edit, IReadOnlyCollection<string> ran)
    {
        var (draft, error) = Read(edit.Yaml);
        if (draft is null) return error;
        if (list.Any(d => d.SpecId == draft.SpecId))
            return $"the series already holds '{draft.SpecId}' — give an appended spec an id of its own, it is minted anew";
        if (draft.Supersedes.Count == 0)
        {
            list.Add(draft);
            return null;
        }
        if (RetireRefusal(list, draft, ran) is { } refused) return refused;
        var at = list.FindLastIndex(d => draft.Supersedes.Contains(d.SpecId, StringComparer.Ordinal));
        list.Insert(at + 1, draft);
        var relink = draft.Supersedes.ToDictionary(id => id, _ => draft.SpecId, StringComparer.Ordinal);
        for (var i = at + 2; i < list.Count; i++)
            if (!ran.Contains(list[i].SpecId) && list[i].Requires.Any(relink.ContainsKey))
                list[i] = list[i] with
                {
                    Requires = [.. list[i].Requires.Select(r => rewriter.RewriteValue(r, relink))],
                    Yaml = rewriter.Rewrite(list[i].Yaml, relink),
                };
        return null;
    }

    private static string? RetireRefusal(List<SpecDraft> list, SpecDraft draft, IReadOnlyCollection<string> ran)
    {
        foreach (var retired in draft.Supersedes)
        {
            if (!list.Any(d => d.SpecId == retired)) return $"it supersedes '{retired}', which the series does not hold";
            if (ran.Contains(retired)) return $"'{retired}' already ran, so it is never retired; a correction to it is a new spec";
            if (list.FirstOrDefault(d => d.Supersedes.Contains(retired, StringComparer.Ordinal)) is { } other)
                return $"'{retired}' is already superseded by '{other.SpecId}'";
            if (draft.Requires.Contains(retired, StringComparer.Ordinal))
                return $"it takes '{retired}''s place, so it cannot require it";
        }
        return null;
    }

    private (SpecDraft? Draft, string? Error) Read(string? yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return (null, "it needs the full spec yaml");
        return validator.ValidateYaml(yaml) is SpecDraftInvalid invalid
            ? (null, $"the yaml is not a valid spec: {invalid.Error}")
            : (reader.Read(yaml), null);
    }
}
