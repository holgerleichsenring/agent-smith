using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services.SpecRename;

/// <summary>2026-10-12-967ab: QueuedTickets — the initial context a queued launch carries.</summary>
public sealed class QueuedTicketSpecNames(RequestContextJsonRewriter requests, RowPages pages) : IStoredSpecNameSurface
{
    public string Name => "QueuedTickets";

    public Task<int> RewriteAsync(AgentSmithDbContext db, CancellationToken ct) =>
        pages.RewriteAsync(db,
            db.QueuedTickets.Where(q => q.InitialContextJson != null && q.InitialContextJson.Contains("hase")),
            q => q.Id, Rewrite, ct);

    private bool Rewrite(QueuedTicket row)
    {
        if (requests.RewriteInitialContext(row.InitialContextJson) is not { } rewritten) return false;
        row.InitialContextJson = rewritten;
        return true;
    }
}
