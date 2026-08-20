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
            return CommandResult.Ok(Untouched(completable.Count));
        if (completable.Count == 0)
            return CommandResult.Ok("No open pull request to complete.");

        var refusals = await CompleteAllAsync(context, completable, cancellationToken);
        return CommandResult.Ok(Report(completable.Count, refusals));
    }

    private async Task<List<string>> CompleteAllAsync(
        InitCompleteContext context, IReadOnlyList<OpenedPullRequest> completable, CancellationToken ct)
    {
        var refusals = new List<string>();
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
            if (!completion.Completed)
                refusals.Add($"{entry.RepoName}: {completion.Reason}");
        }
        return refusals;
    }

    private async Task<PullRequestCompletion> CompleteOneAsync(
        RepoConnection repo, OpenedPullRequest entry, BranchName sourceBranch, CancellationToken ct)
    {
        try
        {
            var completion = await sourceFactory.Create(repo)
                .CompletePullRequestAsync(entry.Url!, sourceBranch, ct);
            if (completion.Completed)
                logger.LogInformation("{Repo}: init PR completed ({Url})", repo.Name, entry.Url);
            else
                logger.LogInformation(
                    "{Repo}: init PR stays open — {Reason}", repo.Name, completion.Reason);
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
                completion.Completed ? PullRequestStatuses.Completed : PullRequestStatuses.CompletionRefused,
                DateTimeOffset.UtcNow, entry.Url, completion.Reason),
            ct);
    }

    private static string Untouched(int count) =>
        count == 0
            ? "Auto-accept was off; no pull request was opened."
            : $"Auto-accept was off; {count} pull request(s) stay open.";

    private static string Report(int attempted, IReadOnlyList<string> refusals) =>
        refusals.Count == 0
            ? $"Completed {attempted} init pull request(s)."
            : $"Completed {attempted - refusals.Count}/{attempted} init pull request(s); "
              + $"still open — {string.Join("; ", refusals)}";
}
