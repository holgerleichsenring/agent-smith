using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: builds the ticket reader a BOUND conversation's turn is seeded with, and
/// answers null for an unbound one — which is what keeps the tool off an unbound turn.
/// <para>
/// A reader needs the project AND the ticket id. The stored row carries the id and no tracker, and
/// the turn carries the project; neither alone can reach a board, so the pairing happens here
/// rather than in the runner, which sits at the per-file limit.
/// </para>
/// </summary>
public sealed class BoundTicketReaders(
    ITicketProviderFactory providers,
    SpecDialogTicketTextRepository store,
    FiledWorkRunsReader runs,
    ILoggerFactory loggerFactory)
{
    public async Task<BoundTicket?> ForAsync(
        string sessionId, ResolvedProject? project, CancellationToken ct)
    {
        if (project is null) return null;
        var held = await store.GetAsync(sessionId, ct);
        if (held is null) return null;
        return new BoundTicket(
            new BoundTicketReader(
                providers, project, held.TicketId, loggerFactory.CreateLogger<BoundTicketReader>()),
            // 2026-09-28-1da5c: and what the TRACKER shows against it. One lookup, two ports —
            // the pairing that made this class exist is the same pairing both of them need.
            new BoundTicketWork(
                providers.CreateLinkedWork(project.Tracker), new TicketId(held.TicketId)),
            // 2026-09-28-1da5d: and what THIS framework did about it, which is a different claim.
            new BoundTicketRuns(
                runs, project, held.TicketId, loggerFactory.CreateLogger<BoundTicketRuns>()));
    }
}

/// <summary>The two things a BOUND turn may ask about its own ticket, from one lookup.</summary>
public sealed record BoundTicket(
    ITicketReader Reader, IBoundTicketWork Work, IBoundTicketRuns Runs);

/// <summary>2026-09-28-1da5c: the linked-work port, closed over the ticket the turn is bound to.</summary>
public sealed class BoundTicketWork(ITicketLinkedWork work, TicketId ticketId) : IBoundTicketWork
{
    public Task<TicketLinkedWorkResult> ForAsync(CancellationToken cancellationToken) =>
        work.ForAsync(ticketId, cancellationToken);
}
