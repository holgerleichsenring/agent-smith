using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-023fb: brings one open agent-smith pull request up to date with the base a landing just moved — merged
/// with <see cref="WorkBranchBaseMerger"/> (state.done collisions united), pushed WITHOUT force, so work another run
/// pushed in the meantime is never overwritten. A conflict leaves the branch as it was and is said on the pull request,
/// once per landing. Only an agent-smith/ branch is written: a handed branch is not licensed for writes.
/// </summary>
public sealed class RefreshBranchBaseHandler(
    WorkBranchBaseMerger merger, GitBranchPusher pusher, IGitTokenResolver credentials,
    SandboxTargets sandboxTargets, ISourceProviderFactory sources, ILogger<RefreshBranchBaseHandler> logger)
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

        var merge = await merger.MergeIntoCurrentAsync(sandbox, ResolvedBase.Rung(baseBranch), cancellationToken);
        return merge.Status switch
        {
            BaseMergeStatus.Merged => await PushAsync(sandbox, repo, branch, merge.BaseRef!, cancellationToken),
            BaseMergeStatus.UpToDate => CommandResult.Ok($"'{branch}' already carries {merge.BaseRef}."),
            BaseMergeStatus.Conflicted => await SayConflictAsync(context, repo, branch, merge, cancellationToken),
            _ => CommandResult.Ok($"'{branch}' was not refreshed: {merge.Reason}"),
        };
    }

    private async Task<CommandResult> PushAsync(
        ISandbox sandbox, Contracts.Models.Configuration.RepoConnection repo, string branch, string baseRef, CancellationToken ct)
    {
        try
        {
            await pusher.PushPlainAsync(sandbox, branch, SandboxGitOperations.CredHelper, credentials.For(repo), ct);
            return CommandResult.Ok($"Merged {baseRef} into '{branch}' and pushed it.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogInformation(ex, "{Branch}: the refreshed branch was not pushed", branch);
            return CommandResult.Ok($"'{branch}' moved on the remote since this run cloned it; the refresh was skipped "
                + "and the branch's next run merges the base itself.");
        }
    }

    private async Task<CommandResult> SayConflictAsync(RefreshBranchBaseContext context,
        Contracts.Models.Configuration.RepoConnection repo, string branch, BaseMergeResult merge, CancellationToken ct)
    {
        var paths = string.Join(", ", merge.ConflictingPaths);
        var text = $"{Marker}\n{merge.BaseRef} moved after a landing, and merging it into `{branch}` conflicts in: {paths}. "
            + "The branch is unchanged; it needs the base merged by hand, or a rework of its ticket.";
        if (context.PrNumber is { Length: > 0 } number && sources.Create(repo) is IPrCommentProvider comments)
        {
            try { await comments.PostCommentAsync(number, text, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "{Repo}: the conflict notice on #{Pr} was not posted", repo.Name, number);
            }
        }
        return CommandResult.Ok($"Merging {merge.BaseRef} into '{branch}' conflicts in {paths}; the branch is unchanged.");
    }
}
