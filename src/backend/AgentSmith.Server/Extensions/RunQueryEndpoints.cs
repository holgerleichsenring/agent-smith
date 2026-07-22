using System.Globalization;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
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
/// </summary>
internal static class RunQueryEndpoints
{
    // Matches the prior Redis-backed Recent window (JobsBroadcaster retained 50;
    // the dashboard caps the visible list client-side at 20/50 under ?debug=1).
    private const int RecentLimit = 50;

    internal static WebApplication MapRunQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs", GetRunsAsync);
        app.MapGet("/api/runs/{runId}", GetRunAsync);
        app.MapGet("/api/runs/{runId}/trail", GetRunTrailAsync);
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

    // p0355: bound for a "load more" page — clamp so a bad/huge limit can't scan away.
    private const int MaxPageLimit = 200;

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
        if (!string.IsNullOrEmpty(before) && TryParseCursor(before, out var cursor))
        {
            var page = await BuildPageBeforeAsync(
                runs, capacityBudget, cursor, Math.Clamp(limit ?? RecentLimit, 1, MaxPageLimit),
                spawner.Value.Resources.MemoryRequest, cancellationToken);
            return Results.Ok(new { recent = page });
        }

        var (active, recent) = await BuildOverviewAsync(
            runs, capacityQueue, cancellationToken, spawner.Value.Resources.MemoryRequest,
            checkpoints, capacityBudget);
        return Results.Ok(new { active, recent });
    }

    private static bool TryParseCursor(string before, out DateTimeOffset cursor) =>
        DateTimeOffset.TryParse(
            before, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out cursor);

    // p0355: map a cursor page of finished runs to snapshots. No queue position or
    // pending question (the page is finished runs), but the capacity footprint is
    // joined so the detail panel stays complete when opened from a paged row.
    internal static async Task<RunSnapshot[]> BuildPageBeforeAsync(
        RunRepository runs, ICapacityBudget? capacityBudget, DateTimeOffset before, int limit,
        string? orchestratorMemoryRequest, CancellationToken ct)
    {
        var page = await runs.GetRunsBeforeAsync(before, limit, ct);
        var footprints = capacityBudget is null
            ? new Dictionary<string, RunCapacitySnapshot>()
            : await capacityBudget.GetManyAsync(page.Select(r => r.Id).ToList(), ct);
        return page.Select(r => RunSnapshotMapper.ToSnapshot(
            r, null, orchestratorMemoryRequest, null, footprints.GetValueOrDefault(r.Id))).ToArray();
    }

    // p0320d: queued runs carry their live 1-based FIFO position, ranked from the
    // capacity queue at query time and matched to run rows via ReservedRunId —
    // never persisted (the head moves). Internal for the endpoint test.
    // p0332: orchestratorMemoryRequest feeds the reserved resource-time — the same
    // JobSpawner Resources value the spawner sizes the orchestrator pod with.
    internal static async Task<(RunSnapshot[] Active, RunSnapshot[] Recent)> BuildOverviewAsync(
        RunRepository runs, ICapacityQueue capacityQueue, CancellationToken cancellationToken,
        string? orchestratorMemoryRequest = null, IRunCheckpointStore? checkpoints = null,
        ICapacityBudget? capacityBudget = null)
    {
        var active = await runs.GetActiveRunsAsync(cancellationToken);
        var recent = await runs.GetRecentRunsAsync(RecentLimit, cancellationToken);
        var positions = await capacityQueue.GetPositionsByRunIdAsync(cancellationToken);
        // p0327: waiting_for_input runs carry their pending question so the list
        // AND the detail (both read this overview) render the answer affordance.
        var pending = await PendingQuestionsByRunIdAsync(checkpoints, cancellationToken);
        // p0336: the capacity footprint for every shown run — the detail panel
        // reads it off the overview, so it must be joined here, not only on detail.
        var footprints = await FootprintsByRunIdAsync(capacityBudget, active, recent, cancellationToken);
        return (
            active.Select(r => RunSnapshotMapper.ToSnapshot(
                r, PositionOf(r, positions), orchestratorMemoryRequest,
                pending.GetValueOrDefault(r.Id), footprints.GetValueOrDefault(r.Id))).ToArray(),
            recent.Select(r => RunSnapshotMapper.ToSnapshot(
                r, PositionOf(r, positions), orchestratorMemoryRequest,
                null, footprints.GetValueOrDefault(r.Id))).ToArray());
    }

    private static async Task<IReadOnlyDictionary<string, RunCapacitySnapshot>> FootprintsByRunIdAsync(
        ICapacityBudget? capacityBudget, List<Run> active, List<Run> recent, CancellationToken ct)
    {
        if (capacityBudget is null) return new Dictionary<string, RunCapacitySnapshot>();
        var ids = active.Concat(recent).Select(r => r.Id).ToList();
        return await capacityBudget.GetManyAsync(ids, ct);
    }

    private static async Task<IReadOnlyDictionary<string, PendingQuestionInfo>> PendingQuestionsByRunIdAsync(
        IRunCheckpointStore? checkpoints, CancellationToken cancellationToken)
    {
        if (checkpoints is null) return new Dictionary<string, PendingQuestionInfo>();
        var pending = await checkpoints.ListPendingAsync(cancellationToken);
        return pending
            .Select(c => (c.RunId, Info: PendingQuestionInfo.FromCheckpoint(c)))
            .Where(x => x.Info is not null)
            .ToDictionary(x => x.RunId, x => x.Info!);
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
            run, PositionOf(run, positions), spawner.Value.Resources.MemoryRequest,
            pendingQuestion, capacity, includeStory: true));
    }

    private static int? PositionOf(Run run, IReadOnlyDictionary<string, int> positions) =>
        run.Status == "queued" && positions.TryGetValue(run.Id, out var position)
            ? position
            : null;
}
