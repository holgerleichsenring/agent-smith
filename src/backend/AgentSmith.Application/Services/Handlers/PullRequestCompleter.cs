using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-023fa: finishes ONE already-opened pull request and records the outcome on the run's per-repo record
/// (p0347's PullRequestsJson, carrying the URL forward) — extracted from InitCompleteHandler so init and a code run's
/// merge name and record an outcome the same way. A provider that throws is a refusal with the message: the work is
/// committed and the pull request open either way.
/// </summary>
public sealed class PullRequestCompleter(
    ISourceProviderFactory sourceFactory, IEventPublisher events, ILogger<PullRequestCompleter> logger)
{
    /// <summary>Completes <paramref name="entry"/>; <paramref name="protection"/> null is init's completion.</summary>
    public async Task<PullRequestCompletion> CompleteAsync(
        PipelineContext pipeline, RepoConnection repo, OpenedPullRequest entry, BranchName branch,
        BranchProtection? protection, CancellationToken ct)
    {
        var completion = await TryCompleteAsync(repo, entry, branch, protection, ct);
        await PublishAsync(pipeline, entry, completion, ct);
        return completion;
    }

    private async Task<PullRequestCompletion> TryCompleteAsync(
        RepoConnection repo, OpenedPullRequest entry, BranchName branch, BranchProtection? protection, CancellationToken ct)
    {
        try
        {
            var provider = sourceFactory.Create(repo);
            var completion = protection is { } mode
                ? await provider.MergePullRequestAsync(entry.Url!, branch, mode, ct)
                : await provider.CompletePullRequestAsync(entry.Url!, branch, ct);
            logger.LogInformation(
                "{Repo}: PR {Outcome} ({Url}){Reason}", repo.Name, completion.Outcome, entry.Url,
                completion.Reason is null ? string.Empty : $" - {completion.Reason}");
            return completion;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Repo}: completing the PR threw", repo.Name);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    /// <summary>Records <paramref name="completion"/> on the run's per-repo record.</summary>
    public async Task PublishAsync(
        PipelineContext pipeline, OpenedPullRequest entry, PullRequestCompletion completion, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        await events.PublishAsync(
            new PullRequestOutcomeEvent(
                runId!, entry.RepoName, InitCompletionReport.StatusOf(completion),
                DateTimeOffset.UtcNow, entry.Url, completion.Reason),
            ct);
    }
}
