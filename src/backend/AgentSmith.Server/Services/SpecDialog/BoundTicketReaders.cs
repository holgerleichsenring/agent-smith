using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
    ILoggerFactory loggerFactory)
{
    public async Task<ITicketReader?> ForAsync(
        string sessionId, ResolvedProject? project, CancellationToken ct)
    {
        if (project is null) return null;
        var held = await store.GetAsync(sessionId, ct);
        return held is null
            ? null
            : new BoundTicketReader(
                providers, project, held.TicketId, loggerFactory.CreateLogger<BoundTicketReader>());
    }
}
