using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: runs the REAL api-security-scan preset against the target this
/// repository serves itself, and scores what it delivered against the declaration.
/// <para>
/// The target is the harness's own loopback server, grown from one health endpoint into a
/// document worth scanning and the behaviour that document describes. The scan reads it
/// through the PRODUCTION swagger provider — the harness's stub answers one invented
/// endpoint whatever it is asked, so under it the served document would never reach the
/// master and the score would be of a fiction.
/// </para>
/// <para>
/// No external machine is involved, and no docker: the dynamic scanners stay stubbed
/// unless an operator opts in, and the score NAMES them as having contributed nothing.
/// </para>
/// </summary>
public static class ApiCorpusEvalHarness
{
    public const string Preset = "api-security-scan";

    public static async Task<ApiCorpusReport> RunAsync(
        ApiTargetDeclaration declaration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        var realScanners = RealCompositionHarness.RealScannersOptedIn();
        await using var target = await StubApiTargetHost.StartAsync(
            FixturePaths.StubApiTargetOpenApi());
        using var scratch = ScratchDirectory.Create("api-corpus");
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), SandboxBackend.Stub, session: null,
            SkillsBackend.Stub, ScanEvalComposition.DrivenByAgentCliAgainstAServedTarget());

        var runner = new PipelineRunner(harness.Services)
        {
            AgentOverride = AgentCliProbe.Agent(),
            RepoOverride = PassiveRepo,
            PassiveRepositoryLocalPath = scratch.Path,
            ApiTargetOverride = target.LoopbackUrl,
            SwaggerPathOverride = target.LoopbackOpenApiUrl,
        };
        var result = await runner.RunAsync(Preset, cancellationToken);
        var pipeline = runner.LastContext!;
        var delivered = Delivered(pipeline);

        return new ApiCorpusReport(
            AgentCliProbe.Model,
            ScanPromptVersion.For(ScanPromptVersion.ApiSecurityMaster),
            DateTimeOffset.UtcNow,
            declaration.Id,
            result.IsSuccess ? ApiCorpusScoring.Score(declaration, delivered) : [],
            ApiScanStepAccount.SilentSteps(pipeline, realScanners),
            result.IsSuccess ? null : result.Message)
        {
            UndeclaredLocations = result.IsSuccess
                ? ApiCorpusScoring.UndeclaredLocations(declaration, delivered)
                : [],
        };
    }

    /// <summary>
    /// The PRODUCTION passive shape: an operator running <c>--target</c> and
    /// <c>--swagger</c> has no source at all. A repo carrying a path that happens to exist
    /// resolves as a local checkout instead, and the strict bootstrap gate then aborts the
    /// run on a tree nobody meant to scan — which is how the first attempt at this scored
    /// nothing.
    /// </summary>
    private static RepoConnection PassiveRepo => new() { Name = "primary", Type = RepoType.Local };

    /// <summary>What the run would hand an operator: the collected, substantiated set.</summary>
    public static IReadOnlyList<SkillObservation> Delivered(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var delivered) && delivered is not null
            ? delivered
            : [];
    }
}
