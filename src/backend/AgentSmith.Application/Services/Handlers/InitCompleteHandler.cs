using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0490: finishes the pull requests InitCommit opened, one repo at a time.
/// 2026-11-17-713ea: always — auto-complete is on for every init, whichever door started it,
/// so the bootstrap lands and a run parked on it restarts by itself. Nobody reviews an init pull request —
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
    PullRequestCompleter completer, // 2026-11-17-023fa: the one completion a code run's merge shares
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
            var completion = await completer.CompleteAsync(context.Pipeline, repo, entry, context.SourceBranch, null, ct);
            if (completion.Outcome is PullRequestCompletionOutcome.Refused)
                refusals.Add($"{entry.RepoName}: {completion.Reason}");
            else if (completion.Outcome is PullRequestCompletionOutcome.Armed)
                armed.Add($"{entry.RepoName}: {completion.Reason}");
        }
        return (refusals, armed);
    }
}
