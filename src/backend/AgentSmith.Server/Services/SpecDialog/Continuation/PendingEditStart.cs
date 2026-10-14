using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// Starting the next attempt over a series edit nobody has worked yet: the pending record is approved again,
/// now by the person asking, with the start the card approval would have carried — the conversation's act,
/// which also answers a waiting hand-back — and <see cref="OperatorStart"/> queues it. A question that waits is
/// answered first and nothing is recorded, as for the card.
/// </summary>
public sealed class PendingEditStart(
    ISpecApprovalStore approvals, OperatorStartArm startArm, OperatorStart starter, ITicketActivityNudge activity,
    TimeProvider clock, ILogger<PendingEditStart> logger)
{
    /// <summary>The sentence the conversation is told.</summary>
    public async Task<string> StartAsync(ResolvedProject project, string ticketId, SpecApprovalRecord pending,
        string conversation, string principal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pending);
        var inputs = await startArm.ReadAsync(project.Name, ticketId, mayStartRuns: true, ct);
        var start = OperatorStartArm.Of(inputs, TicketContinuationEffects.Continue)!;
        if (start.Kind == TicketStartArms.QuestionPending) return OperatorStartArm.Say(start);
        await approvals.SaveAsync(pending with
        {
            Set = pending.Set with { Approval = new SpecApproval(clock.GetUtcNow(), conversation, principal) },
            Continuation = pending.Continuation! with { StartsAttempt = true },
        }, ct);
        logger.LogInformation("Ticket {Ticket}'s pending series edit approved to start by {Principal} in conversation {Conversation}",
            ticketId, principal, conversation);
        await activity.ChangedAsync(project.Name, pending.Key, ct);
        return $"Requested with the edited series — {await starter.StartAsync(project, ticketId, TicketContinuationEffects.Continue, start, ct)}";
    }
}
