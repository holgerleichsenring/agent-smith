using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-13-9a59d: what this attempt served, read from the act slots as they stand once FetchTicket
/// has read them. The one ReworkAct slot already holds the newer of the ticket keyword act and the
/// pull-request act; a pull-request act there wins, a status move back outranks a ticket keyword act,
/// then a run asked for by name, then a re-trigger of a ticket with a previous attempt, else the
/// ticket's first pickup — naming who moved it into its trigger status when the tracker keeps history.
/// <para>2026-10-13-9a59m: an approval in the ticket's own conversation ranks FIRST — the attempt that serves
/// it works the approved revision. FetchTicket runs before the run resolved its approval, so the arm resolves
/// it here (the resolver publishes the winner, which ScopeRepos and DeriveSpec then read unchanged) and
/// applies <see cref="OperatorAct"/> against the previous attempt.</para>
/// <para>2026-10-13-9a59n: a Retry of the previous attempt carries no change, so it ranks below every act with
/// content and above a run asked for by name, a re-trigger and a pickup.</para>
/// <para>2026-10-13-8762p: an overruled default ranks below the act slot and a status move, above Retry.</para>
/// <para>2026-10-13-8762v: a series' automatic start ranks below Retry and above a start by name, which it also is.</para>
/// </summary>
public sealed class AttemptCauseReader(
    TrackerIdentity identity, ApprovedSpecSetResolver approvals, ILogger<AttemptCauseReader> logger)
{
    public async Task<AttemptCause> ReadAsync(
        ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (await OperatorAsync(pipeline, cancellationToken) is { } approved) return Of(approved);
        var act = pipeline.TryGet<ReworkAct>(ContextKeys.ReworkAct, out var a) ? a : null;
        var statusBack = pipeline.TryGet<ReworkAct>(ContextKeys.StatusBackAct, out var s) ? s : null;
        if (act is not null && (act.Channel != ReworkChannel.Ticket || statusBack is null)) return Of(act);
        if (statusBack is not null) return Of(statusBack);
        var previous = pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var p) ? p : null;
        if (OverruleAct.Of(previous) is { } overrule) return Of(overrule);
        if (RetryAct.Of(previous) is { } retry) return Of(retry);
        if (pipeline.IsSeriesAutoStart()) return new AttemptCause(AttemptChannels.Series, AttemptChannels.SeriesActor);
        if (pipeline.IsRequestedByName()) return new AttemptCause(AttemptChannels.Requested);
        if (previous is not null) return new AttemptCause(AttemptChannels.Retrigger);
        return await PickupAsync(provider, context, cancellationToken);
    }

    private async Task<ReworkAct?> OperatorAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (pipeline.TryGet<string>(ContextKeys.PipelineName, out var name) && name is not null && name != PipelinePresets.CodeName)
            return null;
        if (!pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) || ticket is null) return null;
        var record = await approvals.ResolveAsync(pipeline, TicketKeyFactory.For(ticket, pipeline), cancellationToken);
        return OperatorAct.Of(record,
            pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var previous) ? previous : null);
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
        ReworkChannel.Operator => AttemptChannels.Operator,
        ReworkChannel.Retry => AttemptChannels.Retry,
        ReworkChannel.Overrule => AttemptChannels.Overrule,
        _ => AttemptChannels.TicketComment,
    };
}
