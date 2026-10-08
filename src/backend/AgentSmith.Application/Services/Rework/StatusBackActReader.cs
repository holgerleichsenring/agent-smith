using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-2123: the run reads whether a person moved the ticket back into a trigger status after
/// the previous attempt's cutoff, from the tracker's history, and sets ContextKeys.StatusBackAct. No
/// previous attempt, no trigger statuses, a tracker without history: nothing. Fail-soft — a history
/// that cannot be read costs the act, not the run.
/// </summary>
public sealed class StatusBackActReader(TrackerIdentity identity, ILogger<StatusBackActReader> logger)
{
    public async Task ApplyAsync(ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        if (context.TicketId is not { } ticketId || context.TriggerStatuses is not { Count: > 0 } statuses
            || provider is not ITicketStatusHistory history
            || !context.Pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) || attempt is null) return;
        try
        {
            var self = await identity.SelfAsync(context.Config, cancellationToken);
            if (await history.NewestPersonMoveIntoAsync(ticketId, statuses, self, cancellationToken) is { } move && attempt.Precedes(move.At))
                context.Pipeline.Set(ContextKeys.StatusBackAct, new ReworkAct(move.Actor.Login ?? move.Actor.Id, move.At, ReworkChannel.Status));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read the status history of ticket {Ticket} — continuing without it", ticketId.Value);
        }
    }
}
