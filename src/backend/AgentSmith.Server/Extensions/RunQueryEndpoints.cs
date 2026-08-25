using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Events;
using Microsoft.Extensions.Options;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0246f: the dashboard's READ surface for runs, served from the DB
/// system-of-record (RunRepository) — the symmetric GET counterpart to
/// <see cref="RunControlEndpoints"/>' POST cancel. The dashboard fetches the
/// list/detail here on first paint and refetches on the SignalR "RunsChanged"
/// nudge, so Redis carries only transport (live events + the nudge), never the
/// authoritative run data. Survives a process restart AND a Redis flush.
/// <para>
/// p0423b: routing only — composing the list's rows is
/// <see cref="RunListComposer"/>'s job, and the story view's own surface is
/// <see cref="RunStoryEndpoints"/>.
/// </para>
/// </summary>
internal static class RunQueryEndpoints
{
    internal static WebApplication MapRunQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs", GetRunsAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/runs/{runId}", GetRunAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/runs/{runId}/trail", GetRunTrailAsync).Needs(Permissions.RunsRead);
        // p0388b: the full-pipeline read surface (rail, per-step page, decisions)
        // is its own endpoint class — same READ surface, separate responsibility.
        app.MapRunStepQueryEndpoints();
        // p0466: the phase's own read surface — a finished phase is a place to go back to.
        app.MapRunPhaseQueryEndpoints();
        // p0423b: the story view's own surface — statistics folded from the trail
        // and the recorded conversation. Deliberately opened, never pushed.
        app.MapRunStoryEndpoints();
        // 2026-08-25-e257: where the operator says a verdict was wrong. A label, not
        // a control — nothing here moves the run.
        app.MapCriterionJudgementEndpoints();
        return app;
    }

    // p0373: the full-pipeline detail is PULLED from the DB system-of-record, not
    // pushed. Push is reserved for low-frequency lifecycle; the per-action trail
    // grows unbounded with runtime and must not ride the SignalR fan-out. The DB
    // holds every structural event in Seq order and is never evicted — unlike the
    // Redis run stream, whose capped window is rolled over by high-volume stdout.
    // Delta by `sinceSeq` so a poll ships only new events; stdout is excluded at
    // source (not persisted), so this is structural by construction.
    private static async Task<IResult> GetRunTrailAsync(
        string runId, long? sinceSeq, TrailReader trailReader, CancellationToken cancellationToken)
    {
        var page = await trailReader.ReadDbTrailSinceAsync(runId, sinceSeq ?? 0, cancellationToken);
        return Results.Ok(new { events = page.Events, maxSeq = page.MaxSeq });
    }

    private static async Task<IResult> GetRunsAsync(
        RunRepository runs, ICapacityQueue capacityQueue, IRunCheckpointStore checkpoints,
        IOptions<JobSpawnerOptions> spawner, ICapacityBudget capacityBudget,
        string? before, int? limit,
        CancellationToken cancellationToken)
    {
        // p0355: the runs-list "load more" — finished runs OLDER than the `before`
        // ISO-timestamp cursor, newest-first, served from the durable store beyond
        // the retained live window. Returns { recent } only (active runs belong to
        // the first, un-cursored page). An unparseable cursor falls through to the
        // normal overview so a malformed query never 500s the list.
        if (!string.IsNullOrEmpty(before) && RunListComposer.TryParseCursor(before, out var cursor))
        {
            var page = await RunListComposer.BuildPageBeforeAsync(
                runs, capacityBudget, cursor,
                Math.Clamp(limit ?? RunListComposer.RecentLimit, 1, RunListComposer.MaxPageLimit),
                spawner.Value.Resources.MemoryRequest, cancellationToken);
            return Results.Ok(new { recent = page });
        }

        var (active, recent) = await RunListComposer.BuildOverviewAsync(
            runs, capacityQueue, cancellationToken, spawner.Value.Resources.MemoryRequest,
            checkpoints, capacityBudget);
        return Results.Ok(new { active, recent });
    }

    private static async Task<IResult> GetRunAsync(
        string runId, RunRepository runs, ICapacityQueue capacityQueue,
        IRunCheckpointStore checkpoints, IOptions<JobSpawnerOptions> spawner,
        ICapacityBudget capacityBudget, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        if (run is null) return Results.NotFound();
        var positions = await capacityQueue.GetPositionsByRunIdAsync(cancellationToken);
        // p0327: the parked run's pending question rides the detail snapshot so
        // the dashboard renders it with the answer affordance.
        var pendingQuestion = run.Status == "waiting_for_input"
            ? PendingQuestionInfo.FromCheckpoint(
                await checkpoints.GetByRunIdAsync(runId, cancellationToken))
            : null;
        // p0336: the capacity calculation (footprint + reservation) for the panel.
        var capacity = await capacityBudget.GetAsync(runId, cancellationToken);
        // p0344b: the detail additionally serves the persisted run story
        // (progress ledger + acceptance); beats ride list and detail alike.
        return Results.Ok(RunSnapshotMapper.ToSnapshot(
            run, RunListComposer.PositionOf(run, positions), spawner.Value.Resources.MemoryRequest,
            pendingQuestion, capacity, includeStory: true));
    }
}
