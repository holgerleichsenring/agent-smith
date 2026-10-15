using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59m: what a recorded approval does to start the attempt it asks for. A worked ticket gets
/// an Operator nudge — the rework worker serves it under the ticket's lock on whichever replica claims it,
/// and the live run's end nudges again — so there is ONE start path. A ticket no attempt worked has no
/// attempt the worker could reopen, so it is started the way a filed ticket is: tagged and moved.
/// </summary>
public sealed class OperatorStart(
    IReworkNudges nudges, ITicketProviderFactory trackers, FiledWorkMove move, ILogger<OperatorStart> logger,
    UnmetTicketRequirements requirements)
{
    /// <summary>The sentence the thread is told about the start.</summary>
    public async Task<string> StartAsync(
        ResolvedProject project, string ticketId, string effect, TicketContinuationStart start, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(start);
        if (!start.StartsAttempt) return OperatorStartArm.Say(start);
        if (effect == TicketContinuationEffects.AmendNeverWorked)
        {
            var key = AgentSmith.Contracts.Specs.TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
            var (_, unmet) = await requirements.OfAsync(project.Tracker.Name, key, ct); // 2026-10-14-dbceh: the wake starts it
            if (unmet.Count > 0) return RequiresUnmetGate.Sentence(unmet);
            var started = await move.StartExistingAsync(trackers.Create(project.Tracker), project, ticketId, mayStartRuns: true, ct);
            return started.State == FiledStartState.Started
                ? $"Ticket {ticketId}'s first attempt starts: {started.Reason}"
                : $"Ticket {ticketId} was not started: {started.Reason}";
        }
        try
        {
            await nudges.EnqueueAsync(new ReworkNudgeRequest(
                project.Name, ticketId, ReworkNudgeOrigin.Operator, Channel: ReworkChannel.Operator), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Queueing the start of ticket {Ticket} after an approval failed", ticketId);
            return $"The approval is recorded, but its start could not be queued ({ex.Message}); the ticket's next "
                + "run end or a trigger starts it.";
        }
        return OperatorStartArm.Say(start);
    }
}
