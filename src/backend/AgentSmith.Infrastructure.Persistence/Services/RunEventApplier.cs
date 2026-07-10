using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// Maps a typed RunEvent onto its ER entity (the run-level row + its children)
/// over a SCOPED unit of work. Pure projection — no buffering, no Redis. The
/// raw-event trail is the projector's concern; this applies only the events that
/// carry structured run facts the dashboard reads.
/// </summary>
public sealed class RunEventApplier
{
    public async Task ApplyAsync(IUnitOfWork uow, AgentSmith.Contracts.Events.RunEvent ev, CancellationToken ct)
    {
        switch (ev)
        {
            case RunStartedEvent e: await StartRunAsync(uow, e, ct); break;
            case TicketFetchedEvent e: await UpdateRunAsync(uow, e.RunId, r => r.TicketTitle = e.Title, ct); break;
            case RunFinishedEvent e: await FinishRunAsync(uow, e, ct); break;
            case StepStartedEvent e: uow.Add(StepFrom(e)); await uow.SaveChangesAsync(ct); break;
            case StepFinishedEvent e: await FinishStepAsync(uow, e, ct); break;
            case LlmCallFinishedEvent e: uow.Add(LlmFrom(e)); await uow.SaveChangesAsync(ct); break;
            case SandboxCreatedEvent e: uow.Add(SandboxFrom(e)); await uow.SaveChangesAsync(ct); break;
            case SandboxDisposedEvent e: await DisposeSandboxAsync(uow, e, ct); break;
            case DecisionLoggedEvent e: uow.Add(DecisionFrom(e)); await uow.SaveChangesAsync(ct); break;
            case PullRequestOutcomeEvent e: await UpsertRepoAsync(uow, e, ct); break;
            case RunCancelRequestedEvent e: await MarkCancelRequestedAsync(uow, e, ct); break;
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
            if (existing.Status != "queued") return; // duplicate RunStarted replay
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
        });
        foreach (var repo in e.Repos)
            uow.Add(new RunRepo { RunId = e.RunId, RepoName = repo });
        await uow.SaveChangesAsync(ct);
    }

    private static async Task FinishRunAsync(IUnitOfWork uow, RunFinishedEvent e, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == e.RunId, ct);
        if (run is null) return;
        run.Status = e.Status;
        // p0320c: "queued" is a WAITING state, not a terminal one — the row stays
        // in the active set (FinishedAt null) until it launches or is cancelled.
        run.FinishedAt = e.Status == "queued" ? null : e.Timestamp;
        run.Summary = e.Summary;
        if (e.CostUsd is { } cost) run.CostTotalUsd = cost;
        // p0320c TOCTOU backstop: the orchestrator cannot reach this DB, so its
        // capacity rejection surfaces as RunFinished status="queued" — project a
        // queue entry from the run row so the next attempt reuses THIS row.
        if (e.Status == "queued")
            await QueuedRunProjection.UpsertEntryAsync(uow, run, e.Timestamp, ct);
        await uow.SaveChangesAsync(ct);
    }

    // p0259: cancel-requested was trail-only, so a navigated/reloaded detail view
    // (served from this DB projection via RunSnapshotMapper) saw CancelRequested
    // = false and rendered "cancel" instead of "cancelling…". Persisting the flag
    // here is the fix — the canceling state now survives navigation and restart,
    // not just the warm in-memory snapshot.
    private static Task MarkCancelRequestedAsync(IUnitOfWork uow, RunCancelRequestedEvent e, CancellationToken ct) =>
        UpdateRunAsync(uow, e.RunId, r =>
        {
            r.CancelRequested = true;
            r.CancelReason = e.Reason;
        }, ct);

    private static async Task UpdateRunAsync(
        IUnitOfWork uow, string runId, Action<Run> mutate, CancellationToken ct)
    {
        var run = await uow.Set<Run>().FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null) return;
        mutate(run);
        await uow.SaveChangesAsync(ct);
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
        var box = await uow.Set<RunSandbox>()
            .Where(s => s.RunId == e.RunId && s.RepoName == e.Repo)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (box is not null) { box.Status = e.ExitCode == 0 ? "ok" : "failed"; await uow.SaveChangesAsync(ct); }
    }

    private static async Task UpsertRepoAsync(IUnitOfWork uow, PullRequestOutcomeEvent e, CancellationToken ct)
    {
        var repo = await uow.Set<RunRepo>().FirstOrDefaultAsync(r => r.RunId == e.RunId && r.RepoName == e.Repo, ct);
        if (repo is null) { repo = new RunRepo { RunId = e.RunId, RepoName = e.Repo }; uow.Add(repo); }
        repo.PrUrl = e.Url; repo.PrStatus = e.Status; repo.Reason = e.Reason;
        await uow.SaveChangesAsync(ct);
    }

    private static RunStep StepFrom(StepStartedEvent e) =>
        new() { RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.StepName, DisplayName = e.DisplayName, Status = "running" };

    private static RunStep StepFrom(StepFinishedEvent e) =>
        new() { RunId = e.RunId, StepIndex = e.StepIndex, StepName = e.Status, Status = e.Status, DurationSeconds = e.DurationMs / 1000.0, ResultMessage = e.Reason };

    private static RunLlmCall LlmFrom(LlmCallFinishedEvent e) =>
        new() { RunId = e.RunId, Role = e.Role, Phase = e.Phase, Model = e.Model, TokensIn = e.TokensIn, TokensOut = e.TokensOut, CostUsd = e.CostUsd, DurationMs = e.DurationMs };

    private static RunSandbox SandboxFrom(SandboxCreatedEvent e) =>
        new() { RunId = e.RunId, Key = e.Repo, RepoName = e.Repo, ToolchainImage = e.Image, Status = "created" };

    private static RunDecision DecisionFrom(DecisionLoggedEvent e) =>
        new() { RunId = e.RunId, Name = e.Chose, Reason = e.Reason };
}
