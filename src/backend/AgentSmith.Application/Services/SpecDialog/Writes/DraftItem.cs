namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>2026-11-15-06fc: the item a draft path names — its kind, and the spec id or ticket key its file name gives.</summary>
public sealed record DraftItem(DraftItemKind Kind, string Name)
{
    /// <summary>The handle the draft's checks name the item by: a spec's id, a ticket's key, or bug, comment, contract.</summary>
    public string Handle => Kind switch
    {
        DraftItemKind.Bug => ProposalItemRemoval.Bug,
        DraftItemKind.Comment => ProposalItemRemoval.Comment,
        DraftItemKind.Contract => ProposalItemRemoval.Contract,
        _ => Name,
    };
}
