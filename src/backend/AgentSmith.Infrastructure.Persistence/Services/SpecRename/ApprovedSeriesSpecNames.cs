using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-10-12-967ab: ApprovedSeries — the approved record each row keeps whole.</summary>
public sealed class ApprovedSeriesSpecNames(SpecApprovalJsonRewriter approvals, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "ApprovedSeries";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db, db.ApprovedSeries.Where(a => a.ContentJson.Contains("hase")), a => a.Id, Rewrite, ct);

    private bool Rewrite(ApprovedSeries row)
    {
        if (approvals.Rewrite(row.ContentJson) is not { } rewritten) return false;
        row.ContentJson = rewritten;
        return true;
    }
}
