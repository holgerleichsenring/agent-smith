using AgentSmith.Application.Models;
using AgentSmith.Application.Services.BaseRefresh;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-023fb: brings one open agent-smith pull request up to date with the base a landing just moved, pushed
/// WITHOUT force, so work another run pushed in the meantime is never overwritten. Only an agent-smith/ branch is written:
/// a handed branch is not licensed for writes.
/// <para>2026-11-24-6811c: the merge is made whole and checked first (<see cref="BaseRefreshMerge"/>) — conflicts the
/// unions leave go to the base-merge master, every merged tree is built and tested — and pushed only then. A refusal
/// leaves the branch as it was, says why on the pull request, and records refresh_refused with the reason on this run's
/// pull-request outcome, where the ticket's hold reads it.</para>
/// <para>2026-11-24-5ee0: a push is followed up on the pull request (<see cref="RefreshedPullRequestFollowUp"/>): the earlier
/// refusals resolved, the required builds that did not start queued.</para>
/// </summary>
public sealed class RefreshBranchBaseHandler(
    BaseRefreshMerge refresh, GitBranchPusher pusher, IGitTokenResolver credentials, SandboxTargets sandboxTargets,
    ISourceProviderFactory sources, IEventPublisher events, RefreshedPullRequestFollowUp followUp, ILogger<RefreshBranchBaseHandler> logger)
    : ICommandHandler<RefreshBranchBaseContext>
{
    public const string Marker = "<!-- agent-smith:base-refresh -->";

    public async Task<CommandResult> ExecuteAsync(RefreshBranchBaseContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Repos.Count != 1 || context.Branch is not { } branch || context.BaseBranch is not { } baseBranch)
            return CommandResult.Fail("A refresh-base run needs exactly one repository, the work branch and the base branch.");
        if (!WorkBranches.Owns(branch))
            return CommandResult.Fail($"'{branch}' is not an agent-smith work branch; a refresh writes only those.");
        var repo = context.Repos[0];
        if (sandboxTargets.SandboxesForRepo(context.Pipeline, repo) is not [var (_, sandbox), ..])
            return CommandResult.Fail($"{repo.Name}: no sandbox to merge in.");

        var outcome = await refresh.RunAsync(context, repo, sandbox, branch, baseBranch, cancellationToken);
        return outcome.Verdict switch
        {
            BaseRefreshVerdict.Push => await PushAsync(context, sandbox, repo, branch, baseBranch, outcome.Text, cancellationToken),
            BaseRefreshVerdict.Refuse => await RefuseAsync(context, repo, branch, outcome.Text, cancellationToken),
            _ => CommandResult.Ok(outcome.Text),
        };
    }

    private async Task<CommandResult> PushAsync(RefreshBranchBaseContext context,
        ISandbox sandbox, RepoConnection repo, string branch, string baseBranch, string checkedBy, CancellationToken ct)
    {
        try
        {
            await pusher.PushPlainAsync(sandbox, branch, SandboxGitOperations.CredHelper, credentials.For(repo), ct);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(ex, "{Branch}: the refreshed branch was not pushed", branch);
            return CommandResult.Ok($"'{branch}' moved on the remote since this run cloned it; the refresh was skipped "
                + "and the branch's next run merges the base itself.");
        }
        var followed = await followUp.RunAsync(repo, context.PrUrl, context.PrNumber, ct);
        return CommandResult.Ok($"Merged origin/{baseBranch} into '{branch}' ({checkedBy}) and pushed it.{followed}");
    }

    private async Task<CommandResult> RefuseAsync(
        RefreshBranchBaseContext context, RepoConnection repo, string branch, string reason, CancellationToken ct)
    {
        var text = $"{Marker}\n{reason}\n\nThe branch `{branch}` is unchanged; it needs the base merged by hand, or a rework of its ticket.";
        if (context.PrNumber is { Length: > 0 } number && sources.Create(repo) is IPrCommentProvider comments)
        {
            try { await comments.PostCommentAsync(number, text, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "{Repo}: the refresh notice on #{Pr} was not posted", repo.Name, number);
            }
        }
        if (context.Pipeline.TryGet<string>(ContextKeys.RunId, out var runId) && !string.IsNullOrEmpty(runId) && context.PrUrl is { } url)
            await events.PublishAsync(new PullRequestOutcomeEvent(runId!, repo.Name, PullRequestStatuses.RefreshRefused,
                DateTimeOffset.UtcNow, url, reason), ct);
        return CommandResult.Ok($"{reason} The branch is unchanged.");
    }
}
