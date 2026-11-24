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
        ResolvedProject project, RepoConnection repo, RefreshedPullRequest pullRequest, string baseBranch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(pullRequest);
        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        var branch = pullRequest.Branch ?? TicketBranchNamer.Compose(new TicketId(pullRequest.TicketId)).Value; // f9c5f: a series' own
        await runs.CreateQueuedRunAsync(runId, project.Name, PipelinePresets.RefreshBaseName, [repo.Name],
            $"starting — merging {baseBranch} into {branch}", ct,
            new Dictionary<string, string> { [repo.Name] = pullRequest.Url }); // 2026-11-24-6811d: read per pull request
        await dispatch.DispatchAsync(new PipelineRequest(project.Name, PipelinePresets.RefreshBaseName,
            TicketId: null, Headless: true, RunId: runId, Context: Seeds(repo, branch, pullRequest, baseBranch),
            ShownOnTicket: pullRequest.Branch is null ? new TicketId(pullRequest.TicketId) : null), ct); // b34ed: listed on the ticket's page
        logger.LogInformation("Base refresh of {Url} launched (run {RunId})", pullRequest.Url, runId);
        return runId;
    }

    /// <summary>The run's seeds — strings only, so the request survives the queue.</summary>
    public static Dictionary<string, object> Seeds(
        RepoConnection repo, string branch, RefreshedPullRequest pullRequest, string baseBranch) => new()
    {
        [ContextKeys.SourceOverrideRepo] = repo.Name,
        [ContextKeys.CheckoutBranch] = branch,
        [ContextKeys.BaseRefreshPrUrl] = pullRequest.Url,
        [ContextKeys.BaseRefreshPrNumber] = pullRequest.Number,
        [ContextKeys.BaseRefreshBaseBranch] = baseBranch,
    };
}

/// <summary>2026-11-24-297b: the pull request a refresh-base run brings up to date — what the launch needs of its record, so a
/// read that has not been written yet can start one. 2026-11-24-f9c5f: or a series' final pull request, which has no ticket —
/// its head is the series branch (<see cref="Branch"/>).</summary>
public sealed record RefreshedPullRequest(string Url, string Number, string TicketId)
{
    /// <summary>2026-11-24-f9c5f: the head branch when it is no ticket's — a series branch; null for a ticket's pull request.</summary>
    public string? Branch { get; init; }

    /// <summary>2026-11-24-f9c5f: a series' final pull request from its series branch.</summary>
    public static RefreshedPullRequest SeriesFinal(string url, string number, string branch) =>
        new(url, number, string.Empty) { Branch = branch };

    public static RefreshedPullRequest Of(PrReviewRecordView record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new RefreshedPullRequest(record.Url, record.Number, record.TicketId);
    }
}
