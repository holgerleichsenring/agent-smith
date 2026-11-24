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
    TicketProjects projects, Branches.BaseRefreshLauncher refreshes)
    : ISeriesRunLaunch
{
    /// <summary>2026-11-24-f9c5e: the refresh-base run of a series' final pull request, through the launcher every refresh takes.</summary>
    public async Task<string> RefreshAsync(string project, string repo, string branch, string prUrl, string prNumber, string baseBranch,
        CancellationToken cancellationToken)
    {
        var resolved = projects.Of(project) ?? throw new InvalidOperationException($"Project '{project}' is not configured.");
        var connection = resolved.Repos.FirstOrDefault(r => r.Name == repo) ?? throw new InvalidOperationException($"No repo '{repo}' in '{project}'.");
        return await refreshes.LaunchAsync(resolved, connection, branch, prUrl, prNumber, baseBranch, shownOnTicket: null, cancellationToken);
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
