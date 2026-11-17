using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: one file of a design turn's draft written or deleted. The file's text is read into what the
/// existing check of its kind takes — the spec, ticket, bug, comment or contract check the write tools always ran —
/// and the draft with it either comes back whole or not at all, with the exact reason. A file is a view of its item,
/// so nothing beside the typed draft is kept. 2026-11-15-67f1: a ticket, bug or contract keeps the text written as its
/// Source, as a spec keeps its yaml, so the file an edit quotes is the file that was written. 2026-11-17-5701a: a
/// ticket's spec is written and deleted under its ticket.
/// </summary>
public sealed class DraftFileWrite(
    SpecDraftWrite specs, TicketDraftWrite tickets, ContractDraftWrite contracts, TicketSpecDraftWrite ticketSpecs)
{
    public DraftWrite Put(ProposalDraft draft, string path, string text, DraftScope scope)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(scope);
        var bare = DraftPath.Bare(path);
        if (DraftPath.Item(bare) is not { } item) return Outside(bare);
        return item.Kind switch
        {
            DraftItemKind.Spec or DraftItemKind.Epic =>
                specs.Write(draft, item.Name, text ?? string.Empty, item.Kind == DraftItemKind.Epic, scope.Repos),
            DraftItemKind.Ticket => Ticket(draft, item.Name, text, scope),
            DraftItemKind.TicketSpec => ticketSpecs.Write(draft, item.Owner!, item.Name, text ?? string.Empty, scope.Repos),
            DraftItemKind.Bug => Bug(draft, text),
            DraftItemKind.Comment => scope.Bound ? CommentDraftWrite.Write(draft, text)
                : DraftWrite.Refused("a comment belongs to a conversation bound to a ticket, and this one is not"),
            _ => Contract(draft, text),
        };
    }

    public DraftWrite Delete(ProposalDraft draft, string path)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var bare = DraftPath.Bare(path);
        if (DraftPath.IsDirectory(bare)) return DraftDirectoryRemoval.Remove(draft, bare);
        if (DraftPath.Item(bare) is not { } item || DraftFiles.Find(draft, bare) is null)
            return DraftWrite.Refused($"{DraftPath.Shown(bare)} is no file of this draft — it holds {DraftFiles.Listed(draft)}");
        return item.Kind == DraftItemKind.TicketSpec
            ? TicketSpecRemoval.Remove(draft, item.Owner!, item.Name)
            : ProposalItemRemoval.Remove(draft, item.Handle);
    }

    private DraftWrite Ticket(ProposalDraft draft, string key, string text, DraftScope scope)
    {
        var (ticket, error) = DraftTicketText.Read(key, text);
        return ticket is null ? DraftWrite.Refused(error!) : tickets.Write(draft, ticket with { Source = text }, scope.Repos);
    }

    private static DraftWrite Bug(ProposalDraft draft, string text)
    {
        var (title, description, criteria, error) = DraftBugText.Read(text);
        return error is not null ? DraftWrite.Refused(error) : BugDraftWrite.Write(draft, title, description, criteria, text);
    }

    private DraftWrite Contract(ProposalDraft draft, string text)
    {
        var (contract, error) = DraftContractText.Read(text);
        return contract is { } read ? contracts.Write(draft, read, text) : DraftWrite.Refused(error!);
    }

    private static DraftWrite Outside(string bare) =>
        DraftWrite.Refused($"{DraftPath.Shown(bare)} is outside the draft's layout — its files are {DraftPath.Layout}");
}
