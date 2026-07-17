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
        return app;
    }

    private static async Task<IResult> GetRunsAsync(
        RunRepository runs, ICapacityQueue capacityQueue, IRunCheckpointStore checkpoints,
        IOptions<JobSpawnerOptions> spawner, ICapacityBudget capacityBudget,
        CancellationToken cancellationToken)
    {
        var (active, recent) = await BuildOverviewAsync(
            runs, capacityQueue, cancellationToken, spawner.Value.Resources.MemoryRequest,
            checkpoints, capacityBudget);
        return Results.Ok(new { active, recent });
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
