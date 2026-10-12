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
/// </summary>
public sealed class StructureGate(
    SandboxTargets targets, DeliveryDiff deliveryDiff, StructureGitReads git, StructureGateContexts contexts,
    StructureGateFiles files, ILogger<StructureGate> logger)
{
    public async Task<StructureGateVerdict> JudgeAsync(PipelineContext pipeline, CancellationToken ct)
    {
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) || repos is null)
        {
            logger.LogInformation("Structure gate passes: the run names no repository");
            return StructureGateVerdict.Pass;
        }
        var findings = new List<StructureFinding>();
        foreach (var repo in repos)
        {
            var verdict = await JudgeRepoAsync(pipeline, repo, ct);
            if (verdict.Unavailable is not null) return verdict;
            findings.AddRange(verdict.Findings);
        }
        return new StructureGateVerdict(findings);
    }

    private async Task<StructureGateVerdict> JudgeRepoAsync(
        PipelineContext pipeline, RepoConnection repo, CancellationToken ct)
    {
        var owned = targets.SandboxesForRepo(pipeline, repo);
        if (owned.Count == 0) return StructureGateVerdict.Pass;
        // The sandbox RepoWorkPusher puts on the branch: the one whose tree is delivered.
        var sandbox = owned[0].Value;
        var diff = await deliveryDiff.ForBranchAsync(sandbox, DeliveryBasis.For(pipeline), ct);
        if (diff.Failed || diff.BaseRef is not { } baseRef)
        {
            logger.LogInformation("Structure gate passes {Repo}: no comparable base ({Basis})", repo.Name, diff.Basis);
            return StructureGateVerdict.Pass;
        }
        var repoContexts = StructureRepoContexts.Of(pipeline, repo.Name, owned.Select(kv => kv.Key));
        var (owners, rules) = await contexts.ReadAsync(repoContexts, sandbox, baseRef, ct);
        if (rules.Count == 0) return StructureGateVerdict.Pass;
        return await JudgeChangedAsync(repo.Name, sandbox, baseRef, owners, rules, ct);
    }

    private async Task<StructureGateVerdict> JudgeChangedAsync(
        string repoName, ISandbox sandbox, string baseRef, IReadOnlyList<string> owners,
        IReadOnlyDictionary<string, StructureContextRules> rules, CancellationToken ct)
    {
        if (await git.ChangedPathsAsync(sandbox, baseRef, ct) is not { } paths)
        {
            logger.LogWarning(
                "Structure gate could not list what {Repo} changed against {Base}; its structure is not judged",
                repoName, baseRef);
            return StructureGateVerdict.Pass;
        }
        return await files.JudgeAsync(sandbox, paths, owners, rules, ct);
    }
}
