using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: who must act next on a ticket, from its newest attempt. A parked attempt
/// needs a person only when <see cref="RunQueuePlace"/> gives it no place — the gate the runs
/// list and the run detail share — so the ticket list never says needs-you while the runs page
/// says queued to resume.
/// </summary>
public sealed class TicketBucketRule
{
    private const string Running = "running";

    public TicketBucketState Of(Run newest, TicketLiveState live)
    {
        ArgumentNullException.ThrowIfNull(newest);
        var place = RunQueuePlace.Of(newest, live.Positions, live.Relaunching);
        if (newest.Status == RunStatuses.WaitingForInput && place is null)
            return NeedsYou(newest, live);
        if (newest.Status is Running or RunStatuses.Queued or RunStatuses.WaitingForInput)
            return new(TicketBucket.Working, null, newest.StartedAt, null, place, null);
        return new(TicketBucket.Idle, null, newest.FinishedAt, newest.Status, null, null);
    }

    private static TicketBucketState NeedsYou(Run newest, TicketLiveState live) =>
        live.Pending.GetValueOrDefault(newest.Id) is { } question
            ? new(TicketBucket.NeedsYou, TicketNeedsYouKind.Question, question.AskedAt, null, null, question)
            : new(TicketBucket.NeedsYou, TicketNeedsYouKind.Handback, null, null, null, null);
}
