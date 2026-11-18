using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: starts a series' beat or verify on the queued path — the queued run row, then the dispatch, as the
/// base refresh does — seeded with the series id only, a string, so the request survives the queue and any worker
/// takes it.
/// </summary>
public sealed class SeriesRunLaunch(
    InitRunRepository runs, QueuedRunDispatch dispatch, RunRepository rows, TimeProvider clock, ILogger<SeriesRunLaunch> logger)
    : ISeriesRunLaunch
{
    public async Task<string> LaunchAsync(string project, string pipeline, string seriesRunId, CancellationToken cancellationToken)
    {
        var runId = RunIdGenerator.Generate(clock.GetUtcNow());
        await runs.CreateQueuedRunAsync(runId, project, pipeline, [], $"starting — series {seriesRunId}", cancellationToken);
        await dispatch.DispatchAsync(new PipelineRequest(project, pipeline, TicketId: null, Headless: true, RunId: runId,
            Context: new Dictionary<string, object> { [ContextKeys.SeriesRunId] = seriesRunId }), cancellationToken);
        logger.LogInformation("Series {Series}: {Pipeline} run {RunId} launched", seriesRunId, pipeline, runId);
        return runId;
    }

    public async Task<bool> FinishedAsync(string runId, CancellationToken cancellationToken) =>
        await rows.GetRunDetailAsync(runId, cancellationToken) is not { FinishedAt: null };
}
