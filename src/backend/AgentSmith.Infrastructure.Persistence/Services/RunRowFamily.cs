using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59d: the run row's family — the events that change the Run row and the run-level
/// records beside it (checkpoint, ratified expectation, pull-request outcome, classification).
/// </summary>
public sealed class RunRowFamily(
    RunStartProjection starts,
    RunFinalizationProjection finalization,
    RunCheckpointProjection checkpoints,
    RunExpectationProjection expectations,
    RunPullRequestProjection pullRequests,
    RunClassificationProjection classification) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } =
    [
        typeof(RunStartedEvent), typeof(TicketFetchedEvent), typeof(RunFinishedEvent),
        typeof(PullRequestOutcomeEvent), typeof(RunCancelRequestedEvent), typeof(RunCheckpointedEvent),
        typeof(ExpectationRatifiedEvent), typeof(RunStoryRecordedEvent), typeof(PipelineStepsPlannedEvent),
        typeof(RunBudgetResolvedEvent), typeof(RunWorkShapeResolvedEvent),
    ];

    public Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct) => runEvent switch
    {
        RunStartedEvent e => starts.ApplyAsync(uow, e, ct),
        TicketFetchedEvent e => UpdateRunAsync(uow, e.RunId, r => { r.TicketTitle = e.Title; r.ActsReadAt ??= e.ActsReadAt; }, ct),
        RunFinishedEvent e => finalization.ApplyAsync(uow, e, ct),
        PullRequestOutcomeEvent e => pullRequests.ApplyAsync(uow, e, ct),
        RunCancelRequestedEvent e => MarkCancelRequestedAsync(uow, e, ct),
        // p0327 / p0328: the producer may be a spawned orchestrator whose only DB channel is this stream.
        RunCheckpointedEvent e => checkpoints.UpsertAsync(uow, e, ct),
        ExpectationRatifiedEvent e => expectations.UpsertAsync(uow, e, ct),
        // p0344b: the run-story snapshot (progress ledger + acceptance dispositions), served verbatim.
        RunStoryRecordedEvent e => UpdateRunAsync(uow, e.RunId, r =>
        {
            r.ProgressLedgerJson = e.ProgressLedgerJson ?? r.ProgressLedgerJson;
            r.AcceptanceJson = e.AcceptanceJson ?? r.AcceptanceJson;
        }, ct),
        // p0405: the announced command sequence; the latest announcement wins — a splice makes the
        // previous one incomplete, never partially valid.
        PipelineStepsPlannedEvent e => UpdateRunAsync(uow, e.RunId, r =>
        {
            r.PlannedStepsJson = e.StepsJson;
            r.PlannedFirstStepIndex = e.FirstStepIndex;
        }, ct),
        // p0357/p0413: the ticket's size (budget) and its shape (the cut it earned).
        RunBudgetResolvedEvent e => classification.ApplyBudgetAsync(uow, e, ct),
        RunWorkShapeResolvedEvent e => classification.ApplyShapeAsync(uow, e, ct),
        _ => Task.CompletedTask,
    };

    // p0259: the cancel-requested flag survives navigation and restart. p0348: the kill DEADLINE is
    // defaulted when not already set — the cancel enforcer only kills runs that carry one, and `??=`
    // leaves the endpoint's earlier deadline untouched.
    private static Task MarkCancelRequestedAsync(IUnitOfWork uow, RunCancelRequestedEvent e, CancellationToken ct) =>
        UpdateRunAsync(uow, e.RunId, r =>
        {
            r.CancelRequested = true;
            r.CancelReason = e.Reason;
            r.CancelDeadlineAt ??= e.RequestedAt + CancelPolicy.KillGrace;
        }, ct);

    private static async Task UpdateRunAsync(IUnitOfWork uow, string runId, Action<Run> mutate, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        mutate(run);
        await uow.SaveChangesAsync(ct);
    }
}
