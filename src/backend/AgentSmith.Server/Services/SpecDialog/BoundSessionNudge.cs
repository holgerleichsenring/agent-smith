using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-13-9a59g: saves a design conversation and, when it is bound to a ticket, tells that ticket's
/// pages it changed — a turn or a proposal is part of the ticket's conversation. The save is the
/// caller's; the nudge is fail-soft, because a page that refetches late is no reason to lose a turn.
/// </summary>
public sealed class BoundSessionNudge(
    SpecDialogSessionRepository repository, ITicketActivityNudge activity, ILogger<BoundSessionNudge> logger)
{
    public async Task SaveAsync(SpecDialogSession session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        await repository.SaveAsync(ct);
        if (session.TicketKey is not { Length: > 0 } ticketKey) return;
        try
        {
            await activity.ChangedAsync(session.Project, ticketKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Ticket {TicketKey} was not nudged after its conversation was saved", ticketKey);
        }
    }
}
