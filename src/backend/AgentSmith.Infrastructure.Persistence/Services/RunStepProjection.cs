using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-25-61f1: owns the per-step row — its birth at StepStarted and its outcome at
/// StepFinished — split out of <see cref="RunEventApplier"/> like every other projection.
/// It owns the table's identity too: a step row carries the trail position of the event
/// that created it, and an event already recorded here is not recorded again.
/// </summary>
public sealed class RunStepProjection(ProjectedEventRecords records)
{
    // p0322a: besides the step row, persist the producer's live TotalSteps on the run —
    // it's recomputed from the LIVE command list each step and GROWS mid-run (BootstrapDispatch
    // splices rounds), so max() keeps out-of-order replays from shrinking it. Without this the
    // DB projection derived both x and y of "x/y" from the same RunStep rows and the runs list
    // rendered x/x forever.
    public async Task StartAsync(IUnitOfWork uow, StepStartedEvent e, long? eventSeq, CancellationToken ct)
    {
        if (await records.HoldsAsync<RunStep>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(StepFrom(e, eventSeq));
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is not null && e.TotalSteps > (run.TotalSteps ?? 0))
            run.TotalSteps = e.TotalSteps;
        await uow.SaveChangesAsync(ct);
    }

    public async Task FinishAsync(IUnitOfWork uow, StepFinishedEvent e, long? eventSeq, CancellationToken ct)
    {
        var step = await uow.Set<RunStep>()
            .Where(s => s.RunId == e.RunId && s.StepIndex == e.StepIndex)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (step is null) uow.Add(StepFrom(e, eventSeq));
        else { step.Status = e.Status; step.DurationSeconds = e.DurationMs / 1000.0; step.ResultMessage = e.Reason; }
        await uow.SaveChangesAsync(ct);
    }

    // A finish with no start of its own still gets a row, and that row is identified by the
    // finish event that made it — otherwise the next projection of that finish would add a
    // second one.
    private static RunStep StepFrom(StepFinishedEvent e, long? eventSeq) =>
        new()
        {
            RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.Status, Status = e.Status,
            DurationSeconds = e.DurationMs / 1000.0, ResultMessage = e.Reason, EventSeq = eventSeq,
        };

    private static RunStep StepFrom(StepStartedEvent e, long? eventSeq) =>
        new()
        {
            RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.StepName,
            DisplayName = e.DisplayName, Status = "running",
            // p0344b: the typed command name feeds the run-story beat derivation.
            CommandName = e.CommandName,
            PhaseId = e.PhaseId, // p0466: stated by the producer, never parsed back out
            EventSeq = eventSeq,
        };
}
