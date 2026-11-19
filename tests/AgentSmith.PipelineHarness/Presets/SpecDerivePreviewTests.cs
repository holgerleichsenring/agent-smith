using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-26-bdd0d fast-tier end-to-end: a preview derivation records the specs and writes nothing — no spec
/// commit, no spec pull request, no comment on the ticket — and its summary says so. A derivation in branch mode
/// stores the specs and says that. 2026-11-19-6b0ba: the coding master writes the spec from the ticket's stand-in.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDerivePreviewTests
{
    [Fact]
    public async Task SpecDerive_Preview_WritesNoCommitAndOpensNoPullRequest()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        SpecWritingScript.WritesTheSpec(harness);
        var runner = new PipelineRunner(harness.Services);
        runner.RequestContext[ContextKeys.SpecDerivationPreview] = true;

        var result = await runner.RunAsync(PipelinePresets.SpecDeriveName);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("1 spec derived as a preview; nothing was written.");
        harness.StubSandboxFactory!.Spawned.Should().NotContain(s => s.Sandbox.RanSteps.Any(step =>
            step.Command == "git" && step.Args != null && (step.Args.Contains("commit") || step.Args.Contains("push"))),
            "a preview writes nothing to the branch");
        SpecPullRequests.Of(runner.LastContext!).Should().BeEmpty("a preview opens no spec pull request");
        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Specs.Single().Draft.Goal.Should().Be(SpecWritingScript.WrittenGoal, "the coding master wrote the spec");
        set.StandIns.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecDerive_Preview_PostsNoTicketComment()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        SpecWritingScript.WritesTheSpec(harness);
        var runner = new PipelineRunner(harness.Services);
        runner.RequestContext[ContextKeys.SpecDerivationPreview] = true;

        await runner.RunAsync(PipelinePresets.SpecDeriveName);

        tickets.Commented.Should().BeEmpty("a preview says nothing on the ticket");
        tickets.Finalized.Should().BeEmpty();
    }

    [Fact]
    public async Task SpecDerive_TheMasterWritesTheSpec_EndsWithWhereTheSpecsAre()
    {
        var tickets = new RecordingTicketProvider();
        await using var harness = BuildHarness(tickets);
        harness.StubSandboxFactory!.AdvancesHeadOnCommit = true; // the master's revision is a commit of its own
        SpecWritingScript.WritesTheSpec(harness);
        var runner = new PipelineRunner(harness.Services);

        var result = await runner.RunAsync(PipelinePresets.SpecDeriveName);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Be("1 spec derived and stored on the ticket branch.");
        SpecPullRequests.Of(runner.LastContext!).Should().ContainSingle("branch mode opens the draft spec pull request");
        tickets.Finalized.Should().BeEmpty();
    }

    private static RealCompositionHarness BuildHarness(RecordingTicketProvider tickets) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(tickets));
        });
}
