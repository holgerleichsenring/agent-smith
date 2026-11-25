using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-6811c: runs the repository's declared build and test stages over a merged branch before a refresh pushes
/// it — every context in its own toolchain container, stopping at the first red, as a code run's verify does
/// (VerifyStageResolver, VerifyCommandRunner). The tree holds the merge; it is levelled to its commit first (reset and
/// cleaned), so the stages build what would be pushed. 2026-11-25-9992b: every key of the repository works in that one
/// tree of the workspace, so nothing is carried between them.
/// </summary>
public sealed class BaseMergeVerify(
    ContextVerifyStagesResolver declared, VerifyStageResolver stages, VerifyCommandRunner runner,
    SandboxTargets targets, BaseRefreshGit git, ILogger<BaseMergeVerify> logger)
{
    public async Task<BaseMergeVerdict> RunAsync(
        PipelineContext pipeline, RepoConnection repo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var sandboxes = targets.SandboxesForRepo(pipeline, repo);
        if (sandboxes.Count == 0) return BaseMergeVerdict.Unchecked($"{repo.Name}: no sandbox to build in");
        await git.LevelAsync(sandboxes[0].Value, "HEAD", ct);
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
}
