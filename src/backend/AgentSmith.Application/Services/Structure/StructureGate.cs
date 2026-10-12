using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Structure;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-06-8565d: judges every file the branch changed against its context's Limits, minus
/// what the context's baseline tolerates. Terms come from the base; the files come from the
/// working tree, which is what is being delivered. No comparable base, no judgment.
/// </summary>
public sealed class StructureGate(
    SandboxTargets targets, DeliveryDiff deliveryDiff, StructureGitReads git, StructureGateContexts contexts,
    IStructureEngine engine, StructureJudge judge, ISandboxFileReaderFactory files, ILogger<StructureGate> logger)
{
    public async Task<StructureGateVerdict> JudgeAsync(PipelineContext pipeline, CancellationToken ct)
    {
        if (!targets.TryResolve(pipeline, out var sandboxes, out _)) return StructureGateVerdict.Pass;
        var findings = new List<StructureFinding>();
        foreach (var (key, sandbox) in sandboxes)
        {
            var verdict = await JudgeSandboxAsync(pipeline, key, sandbox, ct);
            if (verdict.Unavailable is not null) return verdict;
            findings.AddRange(verdict.Findings);
        }
        return new StructureGateVerdict(findings);
    }

    private async Task<StructureGateVerdict> JudgeSandboxAsync(
        PipelineContext pipeline, string key, ISandbox sandbox, CancellationToken ct)
    {
        var diff = await deliveryDiff.ForBranchAsync(sandbox, DeliveryBasis.For(pipeline), ct);
        if (diff.Failed || diff.BaseRef is not { } baseRef)
        {
            logger.LogInformation("Structure gate passes {Key}: no comparable base ({Basis})", key, diff.Basis);
            return StructureGateVerdict.Pass;
        }
        var (owners, rules) = await contexts.ReadAsync(pipeline, key, sandbox, baseRef, ct);
        var paths = rules.Count == 0 ? [] : await git.ChangedPathsAsync(sandbox, baseRef, ct) ?? [];
        return await JudgeFilesAsync(files.Create(sandbox), paths, owners, rules, ct);
    }

    private async Task<StructureGateVerdict> JudgeFilesAsync(
        ISandboxFileReader reader, IReadOnlyList<string> paths, IReadOnlyList<string> owners,
        IReadOnlyDictionary<string, StructureContextRules> rules, CancellationToken ct)
    {
        var findings = new List<StructureFinding>();
        foreach (var path in paths.Where(engine.Claims))
        {
            if (StructureWorkdir.Owner(owners, path) is not { } owner
                || !rules.TryGetValue(owner, out var terms))
                continue;
            if (await reader.TryReadAsync(path, ct) is not { } content) continue;
            var measurement = engine.Measure(path, content);
            if (measurement.Outcome == StructureOutcome.Unavailable)
                return new StructureGateVerdict([], measurement.Reason);
            findings.AddRange(judge.Judge(path, measurement, terms.Limits).Where(f =>
                !StructureBaselineFile.Tolerates(terms.Baseline, f.Path, f.Key, f.Metric, f.Value)));
        }
        return new StructureGateVerdict(findings);
    }
}
