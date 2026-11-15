using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: a directory of the draft deleted as one change — the whole draft, its specs (with the series
/// contract over them), the epic's parent, or its tickets — so turning a draft into another kind takes one delete.
/// An epic's parent stays while it has several children.
/// </summary>
public static class DraftDirectoryRemoval
{
    public static DraftWrite Remove(ProposalDraft draft, string bare)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return bare.TrimEnd('/') switch
        {
            "" => DraftWrite.To(ProposalDraft.Empty),
            "tickets" => DraftWrite.To(draft with { Tickets = [] }),
            "specs" => DraftWrite.To(draft with { Specs = [.. draft.Specs.Where(s => s.Parent)], Contract = null }),
            _ => Epic(draft),
        };
    }

    private static DraftWrite Epic(ProposalDraft draft)
    {
        var children = draft.Specs.Count(s => !s.Parent);
        return children > 1
            ? DraftWrite.Refused($"the epic's {children} children stay, and several specs are an epic — delete "
                + $"{DraftPath.Prefix}{DraftPath.Specs} too, or keep the parent")
            : DraftWrite.To(draft with { Specs = [.. draft.Specs.Where(s => !s.Parent)], Contract = null });
    }
}
