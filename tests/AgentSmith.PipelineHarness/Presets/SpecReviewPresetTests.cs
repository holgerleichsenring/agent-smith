using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// The spec review inside the real pipeline: it runs between the derivation and the
/// hand-back, and what it finds reaches the contract the run is judged by.
/// <para>
/// The unit tests decide what a finding IS. This decides that the step is wired — that its
/// call happens on a derived spec, that a correction lands on the set the rest of the run
/// reads, and that a criterion no work can satisfy routes into the park instead of buying a
/// master pass. Every other preset gets the review's benign default, so this is the only
/// place the wiring is exercised.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
[Collection("agentsmith-test-env-token")]
public sealed class SpecReviewPresetTests
{
    private const string Shape = "An empty request body is answered with 400.";

    private const string UncorrectableFinding = $$"""
        [{"criterion": "{{Shape}}", "disposition": "prescribes_shape",
          "observation": "search_branch for the request pipeline",
          "output": "this repository has no request pipeline to answer with 400"}]
        """;

    private const string CorrectableFinding = $$"""
        [{"criterion": "{{Shape}}", "disposition": "no_observation_settles",
          "observation": "search_branch for a declared status-code contract",
          "output": "nothing declares what an empty body should answer",
          "replacement": "the empty-body case is covered by a passing test"}]
        """;

    [Fact]
    public async Task SpecReview_ACriterionNoWorkCanSatisfy_ParksInsteadOfReachingTheMaster()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueSpecReviewReply(UncorrectableFinding);
        var runner = new PipelineRunner(harness.Services);

        await runner.RunAsync("fix-bug");

        runner.LastContext.Should().NotBeNull();
        runner.LastContext!.TryGet<SpecHandback>(ContextKeys.SpecHandback, out var handback)
            .Should().BeTrue("a criterion no work can satisfy is what the hand-back exists for");
        handback!.Case.Should().Be(SpecHandbackCase.RequirementsContradictRepository);
        handback.Reason.Should().Contain("no request pipeline",
            "the author is sent the fact that contradicts the criterion, not a complaint");
    }

    [Fact]
    public async Task SpecReview_ACorrectableCriterion_IsReplacedInTheContractTheRunIsJudgedBy()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueSpecReviewReply(CorrectableFinding);
        var runner = new PipelineRunner(harness.Services);

        await runner.RunAsync("fix-bug");

        runner.LastContext!.TryGet<SpecSet>(ContextKeys.SpecSet, out var set).Should().BeTrue();
        set!.Phases[0].Draft.Done.Should().Contain("the empty-body case is covered by a passing test");
        set.Phases[0].Draft.Done.Should().NotContain(Shape);
        runner.LastContext.TryGet<SpecHandback>(ContextKeys.SpecHandback, out _)
            .Should().BeFalse("a correctable finding is corrected, not handed back");
    }
}
