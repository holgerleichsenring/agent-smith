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
/// p0490: finishes the pull requests InitCommit opened, one repo at a time, when the
/// launch carried the operator's auto-accept. Nobody reviews an init pull request —
/// it is generated .agentsmith/ context on a repo that had none — so completing it
/// skips a step that was not happening anyway. A repo whose pull request was skipped
/// or failed is never completed, and a completion a branch policy, a required reviewer
/// or a required build refuses leaves the pull request OPEN with the reason recorded
/// for that repo: the pull request is init's output, not its success criterion (p0321).
/// p0501: a completion can also come back ARMED — accepted, not yet merged, and merging
/// itself when its policy passes. <see cref="InitCompletionReport"/> keeps the three
/// apart in the per-repo record and in the sentence the operator reads.
/// </summary>
public sealed class InitCompleteHandler(
    ISourceProviderFactory sourceFactory,
    IEventPublisher events,
    ILogger<InitCompleteHandler> logger)
    : ICommandHandler<InitCompleteContext>
{
    public async Task<CommandResult> ExecuteAsync(
        InitCompleteContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Pipeline.TryGet<IReadOnlyList<OpenedPullRequest>>(
                ContextKeys.OpenedPullRequests, out var opened) || opened is null)
            return CommandResult.Ok("No OpenedPullRequests in context; nothing to complete.");

        var completable = opened
            .Where(o => o.Status == OpenStatus.Opened && o.Url is not null)
            .ToList();
        if (!context.AutoComplete)
            return CommandResult.Ok(InitCompletionReport.Untouched(completable.Count));
        if (completable.Count == 0)
            return CommandResult.Ok("No open pull request to complete.");

        var (refusals, armed) = await CompleteAllAsync(context, completable, cancellationToken);
        return CommandResult.Ok(InitCompletionReport.Describe(completable.Count, refusals, armed));
    }

    private async Task<(List<string> Refusals, List<string> Armed)> CompleteAllAsync(
        InitCompleteContext context, IReadOnlyList<OpenedPullRequest> completable, CancellationToken ct)
    {
        var refusals = new List<string>();
        var armed = new List<string>();
        foreach (var entry in completable)
        {
            var repo = context.Configs.FirstOrDefault(r => r.Name == entry.RepoName);
            if (repo is null)
            {
                logger.LogWarning("{Repo}: not found in Configs; skip", entry.RepoName);
                continue;
            }
            var completion = await CompleteOneAsync(repo, entry, context.SourceBranch, ct);
            await PublishOutcomeAsync(context.Pipeline, entry, completion, ct);
            if (completion.Outcome is PullRequestCompletionOutcome.Refused)
                refusals.Add($"{entry.RepoName}: {completion.Reason}");
            else if (completion.Outcome is PullRequestCompletionOutcome.Armed)
                armed.Add($"{entry.RepoName}: {completion.Reason}");
        }
        return (refusals, armed);
    }

    private async Task<PullRequestCompletion> CompleteOneAsync(
        RepoConnection repo, OpenedPullRequest entry, BranchName sourceBranch, CancellationToken ct)
    {
        try
        {
            var completion = await sourceFactory.Create(repo)
                .CompletePullRequestAsync(entry.Url!, sourceBranch, ct);
            logger.LogInformation(
                "{Repo}: init PR {Outcome} ({Url}){Reason}", repo.Name, completion.Outcome, entry.Url,
                completion.Reason is null ? string.Empty : $" - {completion.Reason}");
            return completion;
        }
        catch (Exception ex)
        {
            // A provider that throws anyway must not take the run down with it — the
            // files are committed and the pull request is open either way.
            logger.LogWarning(ex, "{Repo}: completing the init PR threw", repo.Name);
            return PullRequestCompletion.Refused(ex.Message);
        }
    }

    // p0490: the completion outcome extends the SAME per-repo record p0347 already
    // keeps (Runs.PullRequestsJson, upserted by repo), carrying the pull request's URL
    // forward so a completed row still links to what was merged.
    private async Task PublishOutcomeAsync(
        PipelineContext pipeline, OpenedPullRequest entry,
        PullRequestCompletion completion, CancellationToken ct)
    {
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return;
        await events.PublishAsync(
            new PullRequestOutcomeEvent(
                runId!, entry.RepoName,
                InitCompletionReport.StatusOf(completion),
                DateTimeOffset.UtcNow, entry.Url, completion.Reason),
            ct);
    }

}
