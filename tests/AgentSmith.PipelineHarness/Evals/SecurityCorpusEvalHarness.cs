using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: runs the REAL security-scan preset over a materialised corpus and
/// scores what it DELIVERED against what the corpus declares.
/// <para>
/// Everything between the corpus and the finding is production code: the preset is the one
/// an operator runs, the scanners read the tree through a sandbox, the master is the
/// packaged security-master answering on a real model, and the delivered set is what
/// SubstantiateFindings left standing. Only the ticket and source providers are the
/// harness's, and neither is on the finding path.
/// </para>
/// </summary>
public static class SecurityCorpusEvalHarness
{
    public const string Preset = "security-scan";

    public static async Task<SecurityCorpusReport> RunAsync(
        IReadOnlyList<SecurityCorpus> corpora, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpora);
        var entries = new List<SecurityCorpusReport.CorpusEntry>();
        foreach (var corpus in corpora)
            entries.Add(await ScoreAsync(corpus, cancellationToken));
        return new SecurityCorpusReport(
            AgentCliProbe.Model, ScanPromptVersion.For(ScanPromptVersion.SecurityMaster),
            DateTimeOffset.UtcNow, entries);
    }

    public static async Task<SecurityCorpusReport.CorpusEntry> ScoreAsync(
        SecurityCorpus corpus, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        await using var tree = await SecurityCorpusTree.MaterialiseAsync(corpus, cancellationToken);
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), SandboxBackend.Stub, session: null,
            SkillsBackend.Stub, ScanEvalComposition.DrivenByAgentCli());

        var runner = new PipelineRunner(harness.Services)
        {
            AgentOverride = AgentCliProbe.Agent(),
            RepoOverride = Repo(tree.Root),
            SourcePathOverride = tree.Root,
        };
        var result = await runner.RunAsync(Preset, cancellationToken);
        var pipeline = runner.LastContext!;

        // A pipeline that did not finish has no score to give. Recording the reason is the
        // whole point: an aborted scan and a scan that found nothing are the same empty set.
        if (!result.IsSuccess)
            return new SecurityCorpusReport.CorpusEntry(
                corpus.Id, [], SecurityScanStepAccount.SilentSteps(pipeline), result.Message);

        return new SecurityCorpusReport.CorpusEntry(
            corpus.Id,
            SecurityCorpusScoring.Score(corpus, Delivered(pipeline)),
            SecurityScanStepAccount.SilentSteps(pipeline),
            null);
    }

    /// <summary>What the run would hand an operator: the merged, substantiated set.</summary>
    public static IReadOnlyList<SkillObservation> Delivered(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var delivered) && delivered is not null
            ? delivered
            : [];
    }

    // Local, so the checkout trusts the tree already on disk; the URL keeps the remote
    // language discovery on the same path every other harness preset run takes.
    private static RepoConnection Repo(string path) => new()
    {
        Name = "primary",
        Type = RepoType.Local,
        Path = path,
        Url = "https://stub.test/primary",
    };
}
