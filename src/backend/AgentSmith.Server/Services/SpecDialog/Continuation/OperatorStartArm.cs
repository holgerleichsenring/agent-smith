using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Contracts.Runs;
using static AgentSmith.Server.Services.SpecDialog.Continuation.TicketStartArms;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59m: the start arm of an approval in a ticket's own conversation. It reads what the rework
/// worker reads — the lease, the newest code attempt, a queued reservation, the attempt's checkpoint — and
/// maps it: a waiting question is answered first, an approver without runs.control records only, a live or
/// queued run is waited for, a hand-back is answered, else the attempt starts now. A ticket no attempt
/// worked starts now or records only: a lease there is the claim window the amendment refuses.
/// </summary>
public sealed class OperatorStartArm(IActiveRunLease leases, IPreviousAttemptReader attempts, IRunCheckpointStore checkpoints)
{
    public async Task<TicketStartInputs> ReadAsync(string project, string ticketId, bool mayStartRuns, CancellationToken ct)
    {
        var live = await leases.GetByTicketAsync(project, new TicketId(ticketId), ct);
        var attempt = await attempts.LatestAsync(project, ticketId, null, ct);
        var waiting = attempt?.Status == RunStatuses.WaitingForInput;
        return new(mayStartRuns, live?.RunId, attempt?.RunId, attempt?.Status,
            await attempts.HasQueuedAsync(project, ticketId, ct),
            waiting && await checkpoints.GetByRunIdAsync(attempt!.RunId, ct) is not null);
    }

    public static TicketContinuationStart? Of(TicketStartInputs? inputs, string effect)
    {
        if (inputs is null) return null;
        if (effect == TicketContinuationEffects.AmendNeverWorked)
            return new(inputs.MayStartRuns ? StartNow : WithoutRunsControl);
        if (effect != TicketContinuationEffects.Continue) return null;
        var waiting = inputs.AttemptStatus == RunStatuses.WaitingForInput;
        if (waiting && inputs.Checkpointed) return new(QuestionPending, inputs.AttemptRunId);
        if (!inputs.MayStartRuns) return new(WithoutRunsControl);
        if (inputs.LiveRunId is not null || inputs.HasQueued) return new(WaitForRun, inputs.LiveRunId);
        return waiting ? new(AnswersHandback, inputs.AttemptRunId) : new(StartNow);
    }

    /// <summary>What the thread is told about the start.</summary>
    public static string Say(TicketContinuationStart start) => start.Kind switch
    {
        QuestionPending => $"Run {start.RunId} is waiting for an answer to its question, so nothing was recorded: answer it "
            + $"first (in the run's question, or POST /api/runs/{start.RunId}/answer), then approve this proposal again.",
        WithoutRunsControl => "You may not start runs (runs.control), so nothing was started and no later run end starts it; "
            + "the next attempt starts when someone who may start runs moves the ticket or asks for it.",
        WaitForRun => start.RunId is { } run
            ? $"Run {run} works this ticket now; the next attempt starts when it ends, unless that run already works this approval."
            : "A queued run of this ticket starts first; the next attempt starts when it ends, unless that run works this approval.",
        AnswersHandback => $"This answers the hand-back of run {start.RunId}: that run is closed and the next attempt starts.",
        _ => "The next attempt starts now.",
    };
}
