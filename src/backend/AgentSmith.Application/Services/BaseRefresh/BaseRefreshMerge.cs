using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-6811c: the refresh-base run's merge, made whole and checked before anything is pushed. The unions resolve
/// the index files; the base-merge master the paths they leave. Every merged tree — clean, united or resolved — is built
/// and tested by the declared stages; a red stage goes back to the same master while StageRepairProgress says the repair
/// moves and the cost budget lasts, its writes amended into the merge commit. Whatever cannot be made whole or checked
/// is refused with its reason and the branch put back at its head. 2026-11-24-9dc0a: the server, not conclude, decides —
/// the conflicts go to <see cref="BaseMergeConflictPass"/>, and a repair is checked by the stages unless the master
/// said whole=false.
/// </summary>
public sealed class BaseRefreshMerge(
    WorkBranchBaseMerger merger, IBaseMergeMaster master, BaseMergeConflictPass conflicts, BaseMergeVerify verify, BaseRefreshGit git)
{
    public async Task<BaseRefreshOutcome> RunAsync(RefreshBranchBaseContext context, RepoConnection repo, ISandbox sandbox,
        string branch, string baseBranch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var resolved = ResolvedBase.Rung(baseBranch);
        if (await git.LineAsync(sandbox, ["rev-parse", "HEAD"], ct) is not { } head)
            return BaseRefreshOutcome.Plain($"'{branch}' was not refreshed: its head cannot be read in this sandbox.");
        var situation = context.Project is { } project ? new BaseMergeSituation(project, context.Pipeline, sandbox, branch,
            resolved.Ref!, await git.SpecsAddedAsync(sandbox, resolved.Ref!, head, ct),
            await git.SpecsAddedAsync(sandbox, head, resolved.Ref!, ct)) : null;
        var merge = await merger.MergeIntoCurrentAsync(sandbox, resolved, ct,
            situation is null ? null : (paths, c) => conflicts.ResolveAsync(situation, paths, c));
        return merge.Status switch
        {
            BaseMergeStatus.Merged => await CheckAsync(context, repo, sandbox, situation, merge, head, ct),
            BaseMergeStatus.UpToDate => BaseRefreshOutcome.Plain($"'{branch}' already carries {merge.BaseRef}."),
            BaseMergeStatus.Conflicted => await RefuseAsync(sandbox, head, $"Merging {merge.BaseRef} into '{branch}' conflicts in "
                + $"{string.Join(", ", merge.ConflictingPaths)}" + (merge.Reason is null ? "." : $"; the master could not resolve it: {merge.Reason}"), ct),
            _ => BaseRefreshOutcome.Plain($"'{branch}' was not refreshed: {merge.Reason}"),
        };
    }

    private async Task<BaseRefreshOutcome> CheckAsync(RefreshBranchBaseContext context, RepoConnection repo, ISandbox sandbox,
        BaseMergeSituation? situation, BaseMergeResult merge, string head, CancellationToken ct)
    {
        string? lastRed = null;
        IReadOnlyList<CodeChange>? lastWrote = null;
        for (var passes = 0; ; passes++)
        {
            var verdict = await verify.RunAsync(context.Pipeline, repo, head, merge.BaseRef!, ct);
            if (verdict.Problem is { } problem) return await RefuseAsync(sandbox, head, $"The merge of {merge.BaseRef} could not be checked: {problem}", ct);
            if (verdict.Red is not { } red)
                return verdict.Ran > 0 ? BaseRefreshOutcome.Push($"{verdict.Ran} declared stage(s) green")
                    : merge.ResolvedByMaster ? await RefuseAsync(sandbox, head, $"A master resolved the conflicts of merging {merge.BaseRef}, "
                        + "and no build or test stage is declared to check that resolution.", ct)
                    : BaseRefreshOutcome.Push("not built: no build or test stage is declared");
            var signature = StageRepairProgress.Signature(red);
            // merge.ci_fix_attempts bounds the passes as it bounds the CI fixes in a row: a stage's output carries times, so
            // "the failure changed" alone would not end a repair that never converges.
            if (situation is null || passes >= situation.Project.Merge.CiFixAttempts
                || (lastRed is not null && !StageRepairProgress.MadeProgress(lastRed, signature, lastWrote))
                || PipelineCostTracker.GetOrCreate(context.Pipeline).IsBudgetExhausted)
                return await RefuseAsync(sandbox, head, Red(merge, red), ct);
            await git.LevelAsync(sandbox, "HEAD", ct); // 9dc0a: the stage's own outputs gone before the master repairs
            var pass = await master.RunAsync(situation with { Red = red }, ct);
            if (pass.Whole == false) return await RefuseAsync(sandbox, head, $"{Red(merge, red)} The master: {pass.Reason}", ct);
            await AmendAsync(sandbox, pass.Wrote, merge.BaseRef!, ct);
            (lastRed, lastWrote) = (signature, pass.Wrote);
        }
    }

    // What the master changed — its file tools' writes, the tracked files it changed by command (a regenerated lockfile, a
    // side taken with git checkout) and what it staged by name — amended into the merge commit, or — when git fast-forwarded
    // and HEAD is the base's own commit — committed on top, never rewriting the base. 2026-11-24-9dc0a: the tree was levelled
    // before the pass, so a stage's outputs are not among them; an untracked file reaches the branch only when staged.
    private async Task AmendAsync(ISandbox sandbox, IReadOnlyList<CodeChange> wrote, string baseRef, CancellationToken ct)
    {
        await git.StageAsync(sandbox, [.. wrote.Select(w => w.Path.Value).Distinct(StringComparer.Ordinal)], ct);
        await git.RunAsync(sandbox, ["add", "-u"], ct);
        if ((await git.RunAsync(sandbox, ["diff", "--cached", "--quiet"], ct)).ExitCode != 1) return;
        var isMerge = (await git.RunAsync(sandbox, ["rev-parse", "-q", "--verify", "HEAD^2"], ct)).ExitCode == 0;
        await git.RunAsync(sandbox, isMerge ? ["commit", "--amend", "--no-edit", "--no-verify"]
            : ["commit", "--no-verify", "-q", "-m", $"Repair the merge of {baseRef}"], ct);
    }

    private static string Red(BaseMergeResult merge, VerifyOutcome red) =>
        $"Merging {merge.BaseRef} turns the {red.Stage} stage red ('{red.Command}', exit {red.ExitCode}). Its output ends:\n"
        + VerifyCommandRunner.Tail(red.Output ?? string.Empty, VerifyCommandRunner.OutputTailChars);

    private async Task<BaseRefreshOutcome> RefuseAsync(ISandbox sandbox, string head, string reason, CancellationToken ct)
    {
        await git.ResetAsync(sandbox, head, ct);
        return BaseRefreshOutcome.Refuse(reason);
    }
}
