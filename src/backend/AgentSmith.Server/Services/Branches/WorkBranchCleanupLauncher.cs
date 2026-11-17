using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-17-713ec: starts a project's branch-cleanup run — the queued row, then the dispatch, as the chat's
/// ticketless launches do. No capacity admission: the run takes no sandbox. A project whose repositories are all
/// local has no host to sweep, and a project whose previous cleanup is still live is left to it.
/// </summary>
public sealed class WorkBranchCleanupLauncher(
    InitRunRepository runs, QueuedRunDispatch dispatch, TimeProvider timeProvider,
    ILogger<WorkBranchCleanupLauncher> logger)
{
    private const string QueuedSummary = "starting — branch cleanup";

    /// <summary>The run id started, or null when the project has nothing to sweep or a sweep is live.</summary>
    public async Task<string?> LaunchAsync(ResolvedProject project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var hosted = project.Repos.Where(r => r.Type != RepoType.Local).Select(r => r.Name).ToList();
        if (hosted.Count == 0) return null;
        if (await runs.FindLiveRunAsync(project.Name, PipelinePresets.BranchCleanupName, ct) is not null) return null;

        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        await runs.CreateQueuedRunAsync(runId, project.Name, PipelinePresets.BranchCleanupName, hosted, QueuedSummary, ct);
        await dispatch.DispatchAsync(new PipelineRequest(
            project.Name, PipelinePresets.BranchCleanupName, TicketId: null, Headless: true, RunId: runId), ct);
        logger.LogInformation("Branch cleanup launched for project {Project} (run {RunId})", project.Name, runId);
        return runId;
    }
}
