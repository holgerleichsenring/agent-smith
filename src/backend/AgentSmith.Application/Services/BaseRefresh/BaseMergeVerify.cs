using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-6811c: runs the repository's declared build and test stages over a merged branch before a refresh pushes
/// it — every context in its own sandbox, stopping at the first red, as a code run's verify does (VerifyStageResolver,
/// VerifyCommandRunner). The primary sandbox holds the merge; it is reset to its commit first, so the stages build what
/// would be pushed. A secondary sandbox (a context on another image) never receives the landed base as a diff, which
/// could pass SandboxTreeGit's limit, and never goes through SandboxTreeSync, which pushes: both compute the same
/// <c>git merge-tree --write-tree</c> of the old head and the base, the secondary reads that tree, and only the change from
/// it to the primary's commit — the resolution and repairs — travels as an encoded diff.
/// </summary>
public sealed class BaseMergeVerify(
    ContextVerifyStagesResolver declared, VerifyStageResolver stages, VerifyCommandRunner runner,
    SandboxTargets targets, SandboxTreeGit tree, BaseRefreshGit git, ILogger<BaseMergeVerify> logger)
{
    public async Task<BaseMergeVerdict> RunAsync(
        PipelineContext pipeline, RepoConnection repo, string head, string baseRef, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var sandboxes = targets.SandboxesForRepo(pipeline, repo);
        if (sandboxes.Count == 0) return BaseMergeVerdict.Unchecked($"{repo.Name}: no sandbox to build in");
        var (primaryKey, primary) = sandboxes[0];
        await git.RunAsync(primary, ["reset", "--hard", "HEAD"], ct);
        foreach (var (key, secondary) in sandboxes.Skip(1))
            if (await LevelAsync(primaryKey, primary, key, secondary, head, baseRef, ct) is { } problem)
                return BaseMergeVerdict.Unchecked(problem);
        var ran = 0;
        foreach (var (key, sandbox) in sandboxes)
        {
            var contexts = declared.For(pipeline, key);
            if (contexts.Count == 0) continue;
            var notes = new VerifyResolutionNotes();
            foreach (var stage in await stages.ResolveAsync(key, null, sandbox, contexts, notes, ct))
            {
                var outcome = await runner.RunAsync(key, stage.Stage, sandbox, stage.Cwd, stage.Command, ct);
                if (outcome.Skipped) continue;
                ran++;
                if (outcome.ExitCode != 0) return new BaseMergeVerdict(ran, outcome, null);
            }
            if (notes.Findings.Count > 0) return BaseMergeVerdict.Unchecked(string.Join(" ", notes.Findings));
        }
        logger.LogInformation("{Repo}: {Count} declared stage(s) green over the merge", repo.Name, ran);
        return new BaseMergeVerdict(ran, null, null);
    }

    private async Task<string?> LevelAsync(string primaryKey, ISandbox primary, string key, ISandbox secondary,
        string head, string baseRef, CancellationToken ct)
    {
        string[] mergeTree = ["merge-tree", "--write-tree", "--no-messages", head, baseRef];
        var ours = await git.LineAsync(primary, mergeTree, ct, tolerateOne: true);
        var theirs = await git.LineAsync(secondary, mergeTree, ct, tolerateOne: true);
        if (ours is null || theirs is null)
            return $"git merge-tree --write-tree failed in {(ours is null ? primaryKey : key)}; the sandbox needs git 2.38 or newer";
        if (ours != theirs) return $"{primaryKey} and {key} merge {baseRef} into different trees";
        await git.RunAsync(secondary, ["reset", "--hard", head], ct);
        if ((await git.RunAsync(secondary, ["read-tree", "--reset", "-u", theirs], ct)) is { ExitCode: not 0 } read)
            return $"{key}: git read-tree of the merge failed: {read.ErrorMessage}";
        string? diff;
        try { diff = await tree.EncodedDiffAsync(primaryKey, primary, ours, "HEAD", [], ct); }
        catch (InvalidOperationException ex) { return ex.Message; }
        if (diff is null) return $"the merge's resolution is larger than the {SandboxTreeGit.EncodedLimit}-character limit a change may cross between {primaryKey} and {key} with";
        return diff.Length == 0 ? null : await tree.ApplyAsync(secondary, diff, checkOnly: false, ct) is { } failed
            ? $"{key}: the merge's resolution did not apply: {failed}" : null;
    }
}
