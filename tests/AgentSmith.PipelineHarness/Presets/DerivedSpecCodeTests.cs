using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0393a through the REAL composition: an ORDINARY ticket — no hand-written spec
/// anywhere — runs the `code` preset end to end because DeriveSpec gives it a stand-in
/// spec first (2026-11-19-6b0ba). This is the spec's headline claim, asserted against the wired pipeline
/// rather than a handler with mocks.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DerivedSpecCodeTests
{

    [Fact]
    public async Task Harness_OrdinaryTicket_RunsCodePresetEndToEndThroughAStandIn()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueFinish();

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.Should().NotBeNull("an ordinary ticket must reach a terminal result");
        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Specs.Should().ContainSingle();
        set.Source.Should().Be(SpecSource.Derived, "the ticket carried no spec of its own");
        set.StandIns.Should().Equal([set.Specs[0].SpecId], "its coding master writes the spec (2026-11-19-6b0ba)");
        runner.LastContext!.Get<SpecDraft>(ContextKeys.CurrentSpec).Done
            .Should().NotBeEmpty("the spec's done-list is the run's acceptance contract");
    }

    [Fact]
    public async Task Harness_TwoSpecDerivation_RunsOneMasterPassPerSpec()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        // p0460: spec 2 is ENTERED over a branch that already carries spec 1's work, and
        // the entry account is what decides whether it is worked at all. This case is about
        // one master pass per spec, so it says plainly that spec 2 is not yet delivered.
        harness.ChatClient
            // spec 1: plan, master, verdict
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueFinish()
            // spec 2: its own plan, its own master pass, its own verdict
            .EnqueueText("Planning: move the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// moved"}""")
            .EnqueueFinish();

        var runner = new PipelineRunner(harness.Services).WithSeries(HarnessSeries.TwoSpecs);
        await runner.RunAsync("code");

        var progress = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);
        progress.Specs.Should().HaveCount(2);
        progress.Specs.Should().OnlyContain(p => p.State == SpecRunState.Done,
            "each spec ends at its own VerifySpec — that is where termination comes from");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "write_file");
    }

}
