using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-11-17-a0b6d: SpecDialogProposal — every stored proposal, whatever its state.</summary>
public sealed class SpecDialogProposalSpecNames(SpecDialogJsonRewriter dialogs, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "SpecDialogProposals";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db, db.Set<SpecDialogProposal>().Where(p => p.ProposalJson.Contains("hase")), p => p.Id, Rewrite, ct);

    private bool Rewrite(SpecDialogProposal row)
    {
        if (dialogs.RewriteOutcome(row.ProposalJson) is not { } proposal) return false;
        row.ProposalJson = proposal;
        return true;
    }
}
