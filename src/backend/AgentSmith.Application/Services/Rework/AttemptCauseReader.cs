using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-13-9a59d: what this attempt served, read from the act slots as they stand once FetchTicket
/// has read them. The one ReworkAct slot already holds the newer of the ticket keyword act and the
/// pull-request act; a pull-request act there wins, a status move back outranks a ticket keyword act,
/// then a run asked for by name, then a re-trigger of a ticket with a previous attempt, else the
/// ticket's first pickup — naming who moved it into its trigger status when the tracker keeps history.
/// </summary>
public sealed class AttemptCauseReader(TrackerIdentity identity, ILogger<AttemptCauseReader> logger)
{
    public async Task<AttemptCause> ReadAsync(
        ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        var act = pipeline.TryGet<ReworkAct>(ContextKeys.ReworkAct, out var a) ? a : null;
        var statusBack = pipeline.TryGet<ReworkAct>(ContextKeys.StatusBackAct, out var s) ? s : null;
        if (act is not null && (act.Channel != ReworkChannel.Ticket || statusBack is null)) return Of(act);
        if (statusBack is not null) return Of(statusBack);
        if (pipeline.IsRequestedByName()) return new AttemptCause(AttemptChannels.Requested);
        if (pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var previous) && previous is not null)
            return new AttemptCause(AttemptChannels.Retrigger);
        return await PickupAsync(provider, context, cancellationToken);
    }

    private async Task<AttemptCause> PickupAsync(
        ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        var pickup = new AttemptCause(AttemptChannels.Pickup);
        if (context.TicketId is not { } ticketId || context.TriggerStatuses is not { Count: > 0 } statuses
            || provider is not ITicketStatusHistory history) return pickup;
        try
        {
            var self = await identity.SelfAsync(context.Config, cancellationToken);
            return await history.NewestPersonMoveIntoAsync(ticketId, statuses, self, cancellationToken) is { } move
                ? pickup with
                {
                    Actor = move.Actor.Login ?? move.Actor.Id, At = move.At,
                    Source = new AttemptSource(AttemptSourceKinds.Status, Detail: move.Status),
                }
                : pickup;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read who moved ticket {Ticket} into its trigger status", ticketId.Value);
            return pickup;
        }
    }

    private static AttemptCause Of(ReworkAct act) => new(Channel(act.Channel), act.Author, act.At, act.Source);

    private static string Channel(ReworkChannel channel) => channel switch
    {
        ReworkChannel.PullRequest => AttemptChannels.PullRequest,
        ReworkChannel.Status => AttemptChannels.StatusMove,
        _ => AttemptChannels.TicketComment,
    };
}
