using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Maps a typed RunEvent onto its ER entity (the run-level row + its children)
/// over a SCOPED unit of work. Pure projection — no buffering, no Redis. The
/// raw-event trail is the projector's concern; this applies only the events that
/// carry structured run facts the dashboard reads.
///
/// p0466: the terminal transition and the phase's standing are projections of
/// their own — see RunFinalizationProjection / RunPhaseProjection.
///
/// <para>2026-08-25-61f1: an event arrives with the position it holds in its run's trail,
/// and every projection that INSERTS stamps it on the row. That position is the row's
/// identity, so the store can refuse to hold one event's record twice.</para>
/// </summary>
public sealed class RunEventApplier(
    RunCheckpointProjection checkpoints,
    RunExpectationProjection expectations,
    QueuedRunProjection queuedRuns,
    RunSandboxProjection sandboxes,
    RunStepProjection steps,
    RunPullRequestProjection pullRequests,
    RunClassificationProjection classification,
    RunFinalizationProjection finalization,
    RunPhaseProjection phases,
    RunLlmCallProjection llmCalls,
    RunDecisionProjection decisions)
{
    /// <summary>
    /// Applies an event whose trail position is not known — the terminal reconciler's path,
    /// which runs outside the drain that mints positions. Nothing it applies inserts a
    /// child row, so no identity is lost by its absence.
    /// </summary>
    public Task ApplyAsync(IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent ev, CancellationToken ct) =>
        ApplyAsync(uow, ev, null, ct);

    public async Task ApplyAsync(
        IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent ev, long? eventSeq, CancellationToken ct)
    {
        switch (ev)
        {
            case RunStartedEvent e: await StartRunAsync(uow, e, ct); break;
            case TicketFetchedEvent e: await UpdateRunAsync(uow, e.RunId, r => r.TicketTitle = e.Title, ct); break;
            case RunFinishedEvent e: await finalization.ApplyAsync(uow, e, ct); break;
            case StepStartedEvent e: await steps.StartAsync(uow, e, eventSeq, ct); break;
            case StepFinishedEvent e: await steps.FinishAsync(uow, e, eventSeq, ct); break;
            case LlmCallFinishedEvent e: await llmCalls.ApplyAsync(uow, e, eventSeq, ct); break;
            // p0369: fold the sandbox command's time/tool-usage/redundancy/build-
            // test facts onto the run's metrics summary (was trail-only before).
            case SandboxResultEvent e: await sandboxes.FoldResultAsync(uow, e, ct); break;
            case SandboxCreatedEvent e: await sandboxes.CreateAsync(uow, e, ct); break;
            case SandboxDisposedEvent e: await sandboxes.DisposeAsync(uow, e, ct); break;
            case SandboxVanishedEvent e: await sandboxes.MarkVanishedAsync(uow, e, ct); break;
            case DecisionLoggedEvent e: await decisions.ApplyAsync(uow, e, eventSeq, ct); break;
            case PullRequestOutcomeEvent e: await pullRequests.ApplyAsync(uow, e, ct); break;
            case RunCancelRequestedEvent e: await MarkCancelRequestedAsync(uow, e, ct); break;
            // p0327: persist the checkpoint (the producer may be a spawned
            // orchestrator whose only DB channel is this event stream).
            case RunCheckpointedEvent e: await checkpoints.UpsertAsync(uow, e, ct); break;
            // p0328: persist the ratified expectation (same spawned-orchestrator
            // constraint — the event stream is the only DB channel).
            case ExpectationRatifiedEvent e: await expectations.UpsertAsync(uow, e, ct); break;
            // p0344b: persist the run-story snapshot (progress ledger + acceptance
            // dispositions) onto the run row — served verbatim on the run detail.
            case RunStoryRecordedEvent e:
                await UpdateRunAsync(uow, e.RunId, r =>
                {
                    r.ProgressLedgerJson = e.ProgressLedgerJson ?? r.ProgressLedgerJson;
                    r.AcceptanceJson = e.AcceptanceJson ?? r.AcceptanceJson;
                }, ct);
                break;
            // p0405: persist the executor's announced command sequence — the run
            // detail's answer to "what is still coming". Latest announcement wins:
            // a splice makes the previous one incomplete, never partially valid.
            case PipelineStepsPlannedEvent e:
                await UpdateRunAsync(uow, e.RunId, r =>
                {
                    r.PlannedStepsJson = e.StepsJson;
                    r.PlannedFirstStepIndex = e.FirstStepIndex;
                }, ct);
                break;
            // p0357/p0413: what the scope classifier decided about the ticket —
            // its size (budget) and its shape (the cut it earned).
            case RunBudgetResolvedEvent e: await classification.ApplyBudgetAsync(uow, e, ct); break;
            case RunWorkShapeResolvedEvent e: await classification.ApplyShapeAsync(uow, e, ct); break;
            // p0466: the phase's own row and the spec it executed — the phase used to
            // survive only as a prefix on a step name, addressable by nothing.
            case PhaseStateChangedEvent e: await phases.ApplyStateAsync(uow, e, ct); break;
            case PhaseRecordedEvent e: await phases.ApplyRecordAsync(uow, e, ct); break;
            default: break; // trail-only event — the projector still persists the raw row
        }
    }

    private async Task StartRunAsync(IUnitOfWork uow, RunStartedEvent e, CancellationToken ct)
    {
        // p0320c: UPSERT — a run launched with a capacity-queue reservation starts
        // on its existing "queued" row, which becomes the running row (one visible
        // row per ticket instead of one per attempt).
        var existing = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (existing is not null)
        {
            // p0327: a resumed run re-launches on its waiting_for_input row the
            // same way a capacity-queued run launches on its queued row.
            if (!RunStatuses.IsWaiting(existing.Status)) return; // duplicate replay
            await queuedRuns.PromoteToRunningAsync(uow, existing, e, ct);
            return;
        }
        uow.Add(new Run
        {
            Id = e.RunId, Pipeline = e.Pipeline, Trigger = e.Trigger, Status = "running",
            TicketId = e.TicketId ?? string.Empty, AgentName = e.AgentName, StartedAt = e.StartedAt,
            // p0320c: project + platform land on the row so the TOCTOU backstop
            // below can key a QueuedTicket entry from the row's own fields.
            Project = e.Project ?? string.Empty, Platform = e.Platform,
            // p0330: the spawn handle rides in on RunStarted — the cancel enforcer
            // force-kills the orchestrator Job/container by this id.
            JobId = e.JobId,
        });
        foreach (var repo in e.Repos)
            uow.Add(new RunRepo { RunId = e.RunId, RepoName = repo });
        await uow.SaveChangesAsync(ct);
    }

    // p0259: cancel-requested was trail-only, so a navigated/reloaded detail view
    // (served from this DB projection via RunSnapshotMapper) saw CancelRequested
    // = false and rendered "cancel" instead of "cancelling…". Persisting the flag
    // here is the fix — the canceling state now survives navigation and restart,
    // not just the warm in-memory snapshot.
    // p0348: also default the kill DEADLINE when it is not already set. The p0330
    // CancelEnforcer only kills runs whose CancelDeadlineAt is non-null, but the
    // deadline was written ONLY on the synchronous endpoint path — a watchdog
    // cancel (or an operator click after the watchdog already flagged the row)
    // reached this projector path with a null deadline and was excluded from
    // enforcement forever, wedging the run in "cancelling…" with an unbounded
    // elapsed. `??=` leaves the endpoint's earlier deadline untouched.
    private static Task MarkCancelRequestedAsync(IUnitOfWork uow, RunCancelRequestedEvent e, CancellationToken ct) =>
        UpdateRunAsync(uow, e.RunId, r =>
        {
            r.CancelRequested = true;
            r.CancelReason = e.Reason;
            r.CancelDeadlineAt ??= e.RequestedAt + CancelPolicy.KillGrace;
        }, ct);

    private static async Task UpdateRunAsync(
        IUnitOfWork uow, string runId, Action<Run> mutate, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        mutate(run);
        await uow.SaveChangesAsync(ct);
    }
}
