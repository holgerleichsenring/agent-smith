using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// The full spec list an edit asks for: its edits applied in order to the series as the next attempt will read it.
/// Every yaml is validated against the spec schema before it is read, a recut keeps its spec's id, and an append
/// may not reuse an id the list holds — its id is minted anew when the edit is decided. The first edit that cannot
/// apply refuses the whole edit.
/// </summary>
public sealed class SeriesEditList(ISpecDraftValidator validator, SpecDraftReader reader)
{
    public (IReadOnlyList<SpecDraft> Drafts, string? Error) Apply(IReadOnlyList<SpecDraft> series, IReadOnlyList<SeriesEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0) return ([], "the edit names no change");
        var list = series.ToList();
        for (var i = 0; i < edits.Count; i++)
        {
            var edit = edits[i];
            var error = edit.Kind switch
            {
                SeriesEditKinds.Recut => Recut(list, edit),
                SeriesEditKinds.Drop => Drop(list, edit),
                SeriesEditKinds.Append => Append(list, edit),
                _ => $"kind '{edit.Kind}' is none of {SeriesEditKinds.Recut}, {SeriesEditKinds.Drop}, {SeriesEditKinds.Append}",
            };
            if (error is not null) return ([], $"edit {i + 1} ({edit.Kind} {edit.SpecId}): {error}");
        }
        return (list, null);
    }

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

    private string? Append(List<SpecDraft> list, SeriesEdit edit)
    {
        var (draft, error) = Read(edit.Yaml);
        if (draft is null) return error;
        if (list.Any(d => d.SpecId == draft.SpecId))
            return $"the series already holds '{draft.SpecId}' — give an appended spec an id of its own, it is minted anew";
        list.Add(draft);
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
