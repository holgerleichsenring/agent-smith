using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-17-023fb: starts one refresh-base run for one open pull request — the queued row, then the dispatch, as
/// the branch-cleanup launch does, so a lost queue push is pushed again from the row. The seeds are strings, scoped to
/// the pull request's repository and its agent-smith/&lt;ticket&gt; branch.
/// </summary>
public sealed class BaseRefreshLauncher(
    InitRunRepository runs, QueuedRunDispatch dispatch, TimeProvider timeProvider, ILogger<BaseRefreshLauncher> logger)
{
    public async Task<string> LaunchAsync(
        ResolvedProject project, RepoConnection repo, PrReviewRecordView pullRequest, string baseBranch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(pullRequest);
        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        var branch = TicketBranchNamer.Compose(new TicketId(pullRequest.TicketId)).Value;
        await runs.CreateQueuedRunAsync(runId, project.Name, PipelinePresets.RefreshBaseName, [repo.Name],
            $"starting — merging {baseBranch} into {branch}", ct);
        await dispatch.DispatchAsync(new PipelineRequest(project.Name, PipelinePresets.RefreshBaseName,
            TicketId: null, Headless: true, RunId: runId, Context: Seeds(repo, branch, pullRequest, baseBranch),
            ShownOnTicket: new TicketId(pullRequest.TicketId)), ct); // 2026-11-17-b34ed: listed on the ticket's page
        logger.LogInformation("Base refresh of {Url} launched (run {RunId})", pullRequest.Url, runId);
        return runId;
    }

    /// <summary>The run's seeds — strings only, so the request survives the queue.</summary>
    public static Dictionary<string, object> Seeds(
        RepoConnection repo, string branch, PrReviewRecordView pullRequest, string baseBranch) => new()
    {
        [ContextKeys.SourceOverrideRepo] = repo.Name,
        [ContextKeys.CheckoutBranch] = branch,
        [ContextKeys.BaseRefreshPrUrl] = pullRequest.Url,
        [ContextKeys.BaseRefreshPrNumber] = pullRequest.Number,
        [ContextKeys.BaseRefreshBaseBranch] = baseBranch,
    };
}
