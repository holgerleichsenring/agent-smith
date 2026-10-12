using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// Judges every file the branch changed against its context's Limits, minus what the context's
/// baseline tolerates — once per repository, in the sandbox the checkpoint pushes, with the
/// terms of every context of that repository. Terms come from the base; the files come from the
/// working tree, which is what is being delivered. No comparable base, no judgment.
/// 2026-10-12-fbbda: every finding carries the repository whose sandbox it was judged in.
/// 2026-10-13-2d0c: the base is the merge-base with the delivery's base rung, so a base that
/// moved after the cut never hands this branch someone else's files; what was not judged is said.
/// </summary>
public sealed class StructureGate(
    SandboxTargets targets, StructureMergeBase mergeBase, StructureGitReads git, StructureRepoContexts repoContexts,
    StructureGateContexts contexts, StructureGateFiles files, ILogger<StructureGate> logger)
{
    public async Task<StructureGateVerdict> JudgeAsync(PipelineContext pipeline, CancellationToken ct)
    {
        var repos = Repos(pipeline);
        var verdict = repos.Count > 0 ? new StructureGateVerdict([])
            : StructureGateVerdict.NotJudged("the run names no repository");
        foreach (var repo in repos)
        {
            var judged = await JudgeRepoAsync(pipeline, repo, ct);
            if (judged.Unavailable is not null) return judged;
            verdict = verdict.Plus(judged);
        }
        return verdict;
    }

    /// <summary>
    /// 2026-10-12-fbbda: the divergences of every repository, judged now on the files the branch
    /// changed — for the pull-request bodies. A repository that cannot be judged names none.
    /// </summary>
    public async Task<IReadOnlyList<StructureFinding>> DivergencesAsync(PipelineContext pipeline, CancellationToken ct)
    {
        var divergences = new List<StructureFinding>();
        foreach (var repo in Repos(pipeline))
            divergences.AddRange((await JudgeRepoAsync(pipeline, repo, ct)).Divergences);
        return divergences;
    }

    private IReadOnlyList<RepoConnection> Repos(PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null)
            return repos;
        logger.LogInformation("Structure gate passes: the run names no repository");
        return [];
    }

    private async Task<StructureGateVerdict> JudgeRepoAsync(
        PipelineContext pipeline, RepoConnection repo, CancellationToken ct)
    {
        var owned = targets.SandboxesForRepo(pipeline, repo);
        if (owned.Count == 0) return StructureGateVerdict.NotJudged($"{repo.Name}: no sandbox");
        // The sandbox RepoWorkPusher puts on the branch: the one whose tree is delivered.
        var sandbox = owned[0].Value;
        if (await mergeBase.ResolveAsync(sandbox, DeliveryBasis.For(pipeline), ct) is not (string rung, string commit))
        {
            logger.LogInformation("Structure gate passes {Repo}: no base rung gives a merge-base", repo.Name);
            return StructureGateVerdict.NotJudged($"{repo.Name}: no comparable base");
        }
        var (owners, rules) = await contexts.ReadAsync(
            repoContexts.Of(pipeline, repo.Name, owned.Select(kv => kv.Key)), sandbox, commit, ct);
        if (rules.Count == 0)
            return StructureGateVerdict.NotJudged($"{repo.Name}: no context with Limits and a baseline at the merge-base with {rung}");
        var verdict = await JudgeChangedAsync(repo.Name, sandbox, commit, owners, rules, ct);
        return verdict with { Findings = [.. verdict.Findings.Select(f => f with { Repository = repo.Name })] };
    }

    private async Task<StructureGateVerdict> JudgeChangedAsync(
        string repoName, ISandbox sandbox, string baseRev, IReadOnlyList<string> owners,
        IReadOnlyDictionary<string, StructureContextRules> rules, CancellationToken ct)
    {
        if (await git.ChangedPathsAsync(sandbox, baseRev, ct) is not { } paths)
        {
            logger.LogWarning(
                "Structure gate could not list what {Repo} changed against {Base}; its structure is not judged",
                repoName, baseRev);
            return StructureGateVerdict.NotJudged($"{repoName}: git could not list the changed files");
        }
        return await files.JudgeAsync(sandbox, paths, owners, rules, ct);
    }
}
