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
        RunRepository runs, ICapacityQueue capacityQueue, IOptions<JobSpawnerOptions> spawner,
        CancellationToken cancellationToken)
    {
        var (active, recent) = await BuildOverviewAsync(
            runs, capacityQueue, cancellationToken, spawner.Value.Resources.MemoryRequest);
        return Results.Ok(new { active, recent });
    }

    // p0320d: queued runs carry their live 1-based FIFO position, ranked from the
    // capacity queue at query time and matched to run rows via ReservedRunId —
    // never persisted (the head moves). Internal for the endpoint test.
    // p0332: orchestratorMemoryRequest feeds the reserved resource-time — the same
    // JobSpawner Resources value the spawner sizes the orchestrator pod with.
    internal static async Task<(RunSnapshot[] Active, RunSnapshot[] Recent)> BuildOverviewAsync(
        RunRepository runs, ICapacityQueue capacityQueue, CancellationToken cancellationToken,
        string? orchestratorMemoryRequest = null)
    {
        var active = await runs.GetActiveRunsAsync(cancellationToken);
        var recent = await runs.GetRecentRunsAsync(RecentLimit, cancellationToken);
        var positions = await capacityQueue.GetPositionsByRunIdAsync(cancellationToken);
        return (
            active.Select(r => RunSnapshotMapper.ToSnapshot(r, PositionOf(r, positions), orchestratorMemoryRequest)).ToArray(),
            recent.Select(r => RunSnapshotMapper.ToSnapshot(r, PositionOf(r, positions), orchestratorMemoryRequest)).ToArray());
    }

    private static async Task<IResult> GetRunAsync(
        string runId, RunRepository runs, ICapacityQueue capacityQueue,
        IOptions<JobSpawnerOptions> spawner, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        if (run is null) return Results.NotFound();
        var positions = await capacityQueue.GetPositionsByRunIdAsync(cancellationToken);
        return Results.Ok(RunSnapshotMapper.ToSnapshot(
            run, PositionOf(run, positions), spawner.Value.Resources.MemoryRequest));
    }

    private static int? PositionOf(Run run, IReadOnlyDictionary<string, int> positions) =>
        run.Status == "queued" && positions.TryGetValue(run.Id, out var position)
            ? position
            : null;
}
