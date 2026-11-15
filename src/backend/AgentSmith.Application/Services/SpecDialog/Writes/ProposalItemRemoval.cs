using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: one item taken out of a draft by its handle — a ticket's key, a spec's id, or bug, comment or
/// contract. An item another one requires stays until that one is rewritten, and an epic's parent stays while it has
/// children, so the draft is never left unfileable. 2026-11-15-06fc: delete_file names the item by its file.
/// </summary>
public static class ProposalItemRemoval
{
    public const string Bug = "bug";
    public const string Comment = "comment";
    public const string Contract = "contract";

    public static DraftWrite Remove(ProposalDraft draft, string handle)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var name = (handle ?? string.Empty).Trim();
        if (name == Bug && draft.Bug is not null) return DraftWrite.To(draft with { Bug = null });
        if (name == Comment && draft.Comment is not null) return DraftWrite.To(draft with { Comment = null });
        if (name == Contract && draft.Contract is not null) return DraftWrite.To(draft with { Contract = null });
        if (draft.Tickets.Any(t => t.Key == name)) return Ticket(draft, name);
        if (draft.Specs.FirstOrDefault(s => s.Draft.SpecId == name) is { } spec) return Spec(draft, spec);
        return DraftWrite.Refused($"'{name}' is no item of this draft — it holds {DraftFiles.Listed(draft)}");
    }

    private static DraftWrite Ticket(ProposalDraft draft, string key)
    {
        var waiting = draft.Tickets.Where(t => t.Requires.Contains(key)).Select(t => t.Key).ToList();
        return waiting.Count > 0
            ? DraftWrite.Refused($"'{string.Join("', '", waiting)}' require '{key}' — edit or delete them first")
            : DraftWrite.To(draft with { Tickets = [.. draft.Tickets.Where(t => t.Key != key)] });
    }

    private static DraftWrite Spec(ProposalDraft draft, DraftSpec spec)
    {
        var id = spec.Draft.SpecId;
        var waiting = draft.Specs.Where(s => s.Draft.Requires.Contains(id)).Select(s => s.Draft.SpecId).ToList();
        if (waiting.Count > 0)
            return DraftWrite.Refused($"'{string.Join("', '", waiting)}' require '{id}' — edit or delete them first");
        if (spec.Parent && draft.Specs.Count(s => !s.Parent) > 1)
            return DraftWrite.Refused($"'{id}' is the parent of {draft.Specs.Count - 1} children — delete them first "
                + $"({DraftPath.Prefix}{DraftPath.Specs} deletes them all), or edit it to change its text");
        return DraftWrite.To(draft with { Specs = [.. draft.Specs.Where(s => s != spec)] });
    }
}
