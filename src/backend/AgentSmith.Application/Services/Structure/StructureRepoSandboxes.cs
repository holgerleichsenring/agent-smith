using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-e237: judges the changed files of each given sandbox of ONE repository against one
/// set of terms read at the base. The first sandbox is judged against the base the terms were read
/// at; every other clone resolves its own merge-base, and one that has none is named as skipped.
/// The same unit seen in two clones is one finding — the worse of the two.
/// </summary>
public sealed class StructureRepoSandboxes(
    StructureMergeBase mergeBase, StructureGitReads git, StructureGateFiles files,
    ILogger<StructureRepoSandboxes> logger)
{
    public async Task<StructureGateVerdict> JudgeAsync(
        PipelineContext pipeline, string repoName, IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes,
        string primaryBase, IReadOnlyList<string> owners, IReadOnlyDictionary<string, StructureContextRules> rules,
        CancellationToken ct)
    {
        var verdict = new StructureGateVerdict([]);
        for (var i = 0; i < sandboxes.Count; i++)
        {
            var (key, sandbox) = (sandboxes[i].Key, sandboxes[i].Value);
            var baseRev = i == 0 ? primaryBase : await SecondaryBaseAsync(pipeline, sandbox, ct);
            var judged = baseRev is null
                ? StructureGateVerdict.NotJudged($"{repoName}: sandbox {key} has no comparable base")
                : await JudgeChangedAsync(repoName, sandbox, baseRev, owners, rules, ct);
            if (judged.Unavailable is not null) return judged;
            verdict = verdict.Plus(judged);
        }
        return verdict with { Findings = Distinct(verdict.Findings), JudgedContexts = rules.Count };
    }

    private async Task<string?> SecondaryBaseAsync(PipelineContext pipeline, ISandbox sandbox, CancellationToken ct) =>
        await mergeBase.ResolveAsync(sandbox, DeliveryBasis.For(pipeline), ct) is (_, string commit) ? commit : null;

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

    // One finding per (path, key, metric), in first-seen order: an excess outranks a divergence,
    // then the larger value — a clone that wrote it worse is the one sent back.
    private static IReadOnlyList<StructureFinding> Distinct(IReadOnlyList<StructureFinding> findings) =>
        [.. findings.GroupBy(f => (f.Path, f.Key, f.Metric)).Select(g => g.MaxBy(f => (f.IsExcess, f.Value))!)];
}
