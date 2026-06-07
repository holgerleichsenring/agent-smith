using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Events;

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

    private static async Task<IResult> GetRunsAsync(RunRepository runs, CancellationToken cancellationToken)
    {
        var active = await runs.GetActiveRunsAsync(cancellationToken);
        var recent = await runs.GetRecentRunsAsync(RecentLimit, cancellationToken);
        return Results.Ok(new
        {
            active = active.Select(RunSnapshotMapper.ToSnapshot).ToArray(),
            recent = recent.Select(RunSnapshotMapper.ToSnapshot).ToArray(),
        });
    }

    private static async Task<IResult> GetRunAsync(
        string runId, RunRepository runs, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunDetailAsync(runId, cancellationToken);
        return run is null ? Results.NotFound() : Results.Ok(RunSnapshotMapper.ToSnapshot(run));
    }
}
