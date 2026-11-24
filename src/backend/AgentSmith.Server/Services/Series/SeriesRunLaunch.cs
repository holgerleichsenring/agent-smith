using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: starts a series' beat or verify on the queued path — the queued run row, then the dispatch, as the
/// base refresh does — seeded with the series id only, a string, so the request survives the queue and any worker
/// takes it.
/// </summary>
public sealed class SeriesRunLaunch(
    InitRunRepository runs, QueuedRunDispatch dispatch, RunRepository rows, TimeProvider clock, ILogger<SeriesRunLaunch> logger,
    TicketProjects projects, Branches.ConflictBaseRefresh conflicts, AgentSmith.Contracts.Providers.ISourceProviderFactory sources)
    : ISeriesRunLaunch
{
    /// <summary>2026-11-24-f9c5f: a series' final pull request that conflicts goes through 2026-11-24-297b's conflict refresh,
    /// the one path a conflicting pull request is brought up to date by.</summary>
    public async Task<bool> RefreshAsync(string project, string repo, string branch, string prUrl, string prNumber,
        DateTimeOffset conflictsSince, CancellationToken cancellationToken)
    {
        if (projects.Of(project) is not { } resolved || resolved.Repos.FirstOrDefault(r => r.Name == repo) is not { } connection) return false;
        return await conflicts.LaunchIfDueAsync(resolved, connection, sources.Create(connection),
            Branches.RefreshedPullRequest.SeriesFinal(prUrl, prNumber, branch), conflictsSince, cancellationToken);
    }

    public async Task<string> LaunchAsync(string project, string pipeline, string seriesRunId, CancellationToken cancellationToken,
        string? checkoutBranch = null)
    {
        var seeds = new Dictionary<string, object> { [ContextKeys.SeriesRunId] = seriesRunId };
        if (!string.IsNullOrWhiteSpace(checkoutBranch)) seeds[ContextKeys.CheckoutBranch] = checkoutBranch; // 2026-11-24-f9c5b
        var runId = RunIdGenerator.Generate(clock.GetUtcNow());
        await runs.CreateQueuedRunAsync(runId, project, pipeline, [], $"starting — series {seriesRunId}", cancellationToken);
        await dispatch.DispatchAsync(new PipelineRequest(project, pipeline, TicketId: null, Headless: true, RunId: runId,
            Context: seeds), cancellationToken);
        logger.LogInformation("Series {Series}: {Pipeline} run {RunId} launched", seriesRunId, pipeline, runId);
        return runId;
    }

    public async Task<bool> FinishedAsync(string runId, CancellationToken cancellationToken) =>
        await rows.GetRunDetailAsync(runId, cancellationToken) is not { FinishedAt: null };
}
