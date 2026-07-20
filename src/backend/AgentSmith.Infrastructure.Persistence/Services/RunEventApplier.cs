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
/// p0336: the terminal-finalize path also releases the run's capacity
/// reservation — this is the single choke point every terminal status flows
/// through (success/failed/cancelled/enforced), so the budget frees exactly when
/// a run stops holding compute. Optional so the many `new RunEventApplier()`
/// test sites keep compiling (they run DB-free, without a budget).
/// </summary>
public sealed class RunEventApplier(ICapacityBudget? capacityBudget = null)
{
    public async Task ApplyAsync(IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent ev, CancellationToken ct)
    {
        switch (ev)
        {
            case RunStartedEvent e: await StartRunAsync(uow, e, ct); break;
            case TicketFetchedEvent e: await UpdateRunAsync(uow, e.RunId, r => r.TicketTitle = e.Title, ct); break;
            case RunFinishedEvent e: await FinishRunAsync(uow, e, ct); break;
            case StepStartedEvent e: await StartStepAsync(uow, e, ct); break;
            case StepFinishedEvent e: await FinishStepAsync(uow, e, ct); break;
            case LlmCallFinishedEvent e: await ApplyLlmCallAsync(uow, e, ct); break;
            case SandboxCreatedEvent e: uow.Add(SandboxFrom(e)); await uow.SaveChangesAsync(ct); break;
            case SandboxDisposedEvent e: await DisposeSandboxAsync(uow, e, ct); break;
            case SandboxVanishedEvent e: await MarkSandboxVanishedAsync(uow, e, ct); break;
            case DecisionLoggedEvent e: uow.Add(DecisionFrom(e)); await uow.SaveChangesAsync(ct); break;
            case PullRequestOutcomeEvent e: await ApplyPullRequestOutcomeAsync(uow, e, ct); break;
            case RunCancelRequestedEvent e: await MarkCancelRequestedAsync(uow, e, ct); break;
            // p0327: persist the checkpoint (the producer may be a spawned
            // orchestrator whose only DB channel is this event stream).
            case RunCheckpointedEvent e: await RunCheckpointProjection.UpsertAsync(uow, e, ct); break;
            // p0328: persist the ratified expectation (same spawned-orchestrator
            // constraint — the event stream is the only DB channel).
            case ExpectationRatifiedEvent e: await RunExpectationProjection.UpsertAsync(uow, e, ct); break;
            // p0344b: persist the run-story snapshot (progress ledger + acceptance
            // dispositions) onto the run row — served verbatim on the run detail.
            case RunStoryRecordedEvent e:
                await UpdateRunAsync(uow, e.RunId, r =>
                {
                    r.ProgressLedgerJson = e.ProgressLedgerJson ?? r.ProgressLedgerJson;
                    r.AcceptanceJson = e.AcceptanceJson ?? r.AcceptanceJson;
                }, ct);
                break;
            default: break; // trail-only event — the projector still persists the raw row
        }
    }

    private static async Task StartRunAsync(IUnitOfWork uow, RunStartedEvent e, CancellationToken ct)
    {
        // p0320c: UPSERT — a run launched with a capacity-queue reservation starts
        // on its existing "queued" row, which becomes the running row (one visible
        // row per ticket instead of one per attempt).
        var existing = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (existing is not null)
        {
            // p0327: a resumed run re-launches on its waiting_for_input row the
            // same way a capacity-queued run launches on its queued row.
            if (existing.Status is not ("queued" or "waiting_for_input")) return; // duplicate replay
            await QueuedRunProjection.PromoteToRunningAsync(uow, existing, e, ct);
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

    private async Task FinishRunAsync(IUnitOfWork uow, RunFinishedEvent e, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is null) return;
        // p0330: terminal transitions are SET-ONCE. 'queued' keeps FinishedAt null
        // (it is a WAITING state, see below), so a non-null FinishedAt means a
        // terminal status already landed — a late RunFinished from a force-killed
        // pod must not overwrite 'cancelled', and vice versa.
        if (run.FinishedAt is not null) return;
        run.Status = e.Status;
        // p0320c: "queued" is a WAITING state, not a terminal one — the row stays
        // in the active set (FinishedAt null) until it launches or is cancelled.
        // p0327: "waiting_for_input" is the same shape — parked on a question,
        // no lease, no sandbox, resumed onto this very row.
        run.FinishedAt = e.Status is "queued" or "waiting_for_input" ? null : e.Timestamp;
        run.Summary = e.Summary;
        // p0355: cost must be TRUE on revisit. The run-end total (RunFinishedEvent.
        // CostUsd) is authoritative when present, but older/leaking producers emit
        // null — and the DB projector never accumulated per-call cost onto the row,
        // so those runs persisted $0 despite real RunLlmCall rows. Fall back to the
        // sum of the persisted per-call costs so the detail read returns the real
        // total, not a stale zero.
        run.CostTotalUsd = e.CostUsd ?? await SumLlmCostAsync(uow, e.RunId, ct);
        // p0320c TOCTOU backstop: the orchestrator cannot reach this DB, so its
        // capacity rejection surfaces as RunFinished status="queued" — project a
        // queue entry from the run row so the next attempt reuses THIS row.
        if (e.Status == "queued")
            await QueuedRunProjection.UpsertEntryAsync(uow, run, e.Timestamp, ct);
        await uow.SaveChangesAsync(ct);
        // p0336: a terminal run stops holding compute — free its budget reservation.
        // A waiting state (queued / waiting_for_input) keeps FinishedAt null and its
        // reservation, so the run is guaranteed its footprint when it (re)launches.
        if (run.FinishedAt is not null && capacityBudget is not null)
            await capacityBudget.ReleaseAsync(e.RunId, ct);
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

    // p0322a: besides the step row, persist the producer's live TotalSteps on the
    // run — it's recomputed from the LIVE command list each step and GROWS mid-run
    // (BootstrapDispatch splices rounds), so max() keeps out-of-order replays from
    // shrinking it. Without this the DB projection derived both x and y of "x/y"
    // from the same RunStep rows and the runs list rendered x/x forever.
    private static async Task StartStepAsync(IUnitOfWork uow, StepStartedEvent e, CancellationToken ct)
    {
        uow.Add(StepFrom(e));
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is not null && e.TotalSteps > (run.TotalSteps ?? 0))
            run.TotalSteps = e.TotalSteps;
        await uow.SaveChangesAsync(ct);
    }

    // p0355: besides the per-call row, accumulate the call's cost onto the run
    // row LIVE — a RUNNING run's snapshot shows the spend already made instead
    // of $0.00 until finish. The finish path stays authoritative: RunFinished
    // overwrites the row with its own total (or the per-call sum fallback), and
    // a terminal row (FinishedAt set) is never mutated by a late replay.
    private static async Task ApplyLlmCallAsync(IUnitOfWork uow, LlmCallFinishedEvent e, CancellationToken ct)
    {
        uow.Add(LlmFrom(e));
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is not null && run.FinishedAt is null)
            run.CostTotalUsd += e.CostUsd;
        await uow.SaveChangesAsync(ct);
    }

    // p0355: sum the run's persisted per-call costs — the fallback total when the
    // run-end event carried no cost. Zero when no calls were recorded.
    private static async Task<decimal> SumLlmCostAsync(IUnitOfWork uow, string runId, CancellationToken ct)
    {
        var costs = await uow.Set<RunLlmCall>().AsNoTracking()
            .Where(c => c.RunId == runId).Select(c => c.CostUsd).ToListAsync(ct);
        return costs.Sum();
    }

    private static async Task FinishStepAsync(IUnitOfWork uow, StepFinishedEvent e, CancellationToken ct)
    {
        var step = await uow.Set<RunStep>()
            .Where(s => s.RunId == e.RunId && s.StepIndex == e.StepIndex)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (step is null) uow.Add(StepFrom(e));
        else { step.Status = e.Status; step.DurationSeconds = e.DurationMs / 1000.0; step.ResultMessage = e.Reason; }
        await uow.SaveChangesAsync(ct);
    }

    private static async Task DisposeSandboxAsync(IUnitOfWork uow, SandboxDisposedEvent e, CancellationToken ct)
    {
        var box = await LatestSandboxAsync(uow, e.RunId, e.Repo, ct);
        if (box is null) return;
        box.Status = e.ExitCode == 0 ? "ok" : "failed";
        // p0332: the dispose timestamp closes the sandbox lifetime window.
        box.DisposedAt ??= e.Timestamp;
        await uow.SaveChangesAsync(ct);
    }

    // p0332: a vanished sandbox (heartbeat gone + container confirmed dead) never
    // gets a SandboxDisposedEvent — the vanish verdict IS its end-of-life, so it
    // closes the lifetime window too. Was trail-only before p0332.
    private static async Task MarkSandboxVanishedAsync(IUnitOfWork uow, SandboxVanishedEvent e, CancellationToken ct)
    {
        var box = await LatestSandboxAsync(uow, e.RunId, e.Repo, ct);
        if (box is null) return;
        box.Status = "vanished";
        box.DisposedAt ??= e.Timestamp;
        await uow.SaveChangesAsync(ct);
    }

    private static Task<RunSandbox?> LatestSandboxAsync(IUnitOfWork uow, string runId, string repo, CancellationToken ct) =>
        uow.Set<RunSandbox>()
            .Where(s => s.RunId == runId && s.RepoName == repo)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);

    // p0347: a PR outcome lands in TWO durable places — the per-repo RunRepo row
    // (feeds the run-snapshot PrUrl + beats) and the Runs.PullRequestsJson list
    // (the durable, timestamped, multi-repo-complete history the Pull Requests
    // page + run detail read). Same event, one apply.
    private static async Task ApplyPullRequestOutcomeAsync(IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        await UpsertRepoAsync(uow, e, ct);
        await UpsertPullRequestJsonAsync(uow, e, ct);
    }

    private static async Task UpsertRepoAsync(IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        var repo = await uow.Set<RunRepo>().FirstOrDefaultAsync(r => r.RunId == e.RunId && r.RepoName == e.Repo, ct);
        if (repo is null) { repo = new RunRepo { RunId = e.RunId, RepoName = e.Repo }; uow.Add(repo); }
        repo.PrUrl = e.Url; repo.PrStatus = e.Status; repo.Reason = e.Reason;
        await uow.SaveChangesAsync(ct);
    }

    // p0347: fold the outcome into the run's PullRequestsJson list, upserting by
    // repo (the last outcome per repo wins — a retried commit/PR step overwrites
    // the earlier attempt). The stored camelCase JSON IS the wire payload the
    // dashboard reads, matching the p0344b run-story pattern.
    private static Task UpsertPullRequestJsonAsync(IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct) =>
        UpdateRunAsync(uow, e.RunId, run =>
        {
            var prs = RunStoryJson.TryDeserialize<List<RunPullRequestView>>(run.PullRequestsJson)
                ?? new List<RunPullRequestView>();
            prs.RemoveAll(p => p.Repo == e.Repo);
            prs.Add(new RunPullRequestView(e.Repo, e.Status, e.Url, e.Reason, e.Timestamp));
            run.PullRequestsJson = RunStoryJson.Serialize(prs);
        }, ct);

    private static RunStep StepFrom(StepStartedEvent e) =>
        new()
        {
            RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.StepName,
            DisplayName = e.DisplayName, Status = "running",
            // p0344b: the typed command name feeds the run-story beat derivation.
            CommandName = e.CommandName,
        };

    private static RunStep StepFrom(StepFinishedEvent e) =>
        new() { RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.Status, Status = e.Status, DurationSeconds = e.DurationMs / 1000.0, ResultMessage = e.Reason };

    private static RunLlmCall LlmFrom(LlmCallFinishedEvent e) =>
        new()
        {
            RunId = e.RunId, Role = e.Role, Phase = e.Phase, Model = e.Model,
            TokensIn = e.TokensIn, TokensOut = e.TokensOut, CostUsd = e.CostUsd, DurationMs = e.DurationMs,
            CachedTokensIn = e.CachedTokensIn, CacheCreationTokensIn = e.CacheCreationTokensIn,
        };

    // p0332: lifetime start + declared memory request land on the row so the
    // snapshot can compute reserved resource-time (request x lifetime) per run.
    private static RunSandbox SandboxFrom(SandboxCreatedEvent e) =>
        new()
        {
            RunId = e.RunId, Key = e.Repo, RepoName = e.Repo, ToolchainImage = e.Image, Status = "created",
            SpawnedAt = e.Timestamp, MemoryRequest = e.MemoryRequest,
        };

    private static RunDecision DecisionFrom(DecisionLoggedEvent e) =>
        new() { RunId = e.RunId, Name = e.Chose, Reason = e.Reason };
}
