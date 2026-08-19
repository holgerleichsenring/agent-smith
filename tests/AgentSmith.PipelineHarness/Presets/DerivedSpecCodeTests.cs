using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0393a through the REAL composition: an ORDINARY ticket — no hand-written spec
/// anywhere — runs the `code` preset end to end because DeriveSpec turns it into phase
/// specs first. This is the phase's headline claim, asserted against the wired pipeline
/// rather than a handler with mocks.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DerivedSpecCodeTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"},{"criterion":"criterion 2","status":"met","evidence":"preserved"}]}""";

    [Fact]
    public async Task Harness_OrdinaryTicket_RunsCodePresetEndToEndThroughDerivation()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet build","repo":"primary"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        result.Should().NotBeNull("an ordinary ticket must reach a terminal result");
        var set = runner.LastContext!.Get<SpecSet>(ContextKeys.SpecSet);
        set.Phases.Should().ContainSingle();
        set.Source.Should().Be(SpecSource.Derived, "the ticket carried no spec of its own");
        runner.LastContext!.Get<PhaseDraft>(ContextKeys.PhaseSpec).Done
            .Should().NotBeEmpty("the phase's done-list is the run's acceptance contract");
    }

    [Fact]
    public async Task Harness_TwoPhaseDerivation_RunsOneMasterPassPerPhase()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        // p0460: phase 2 is ENTERED over a branch that already carries phase 1's work, and
        // the entry account is what decides whether it is worked at all. This case is about
        // one master pass per phase, so it says plainly that phase 2 is not yet delivered.
        harness.Services.GetRequiredService<HarnessSpecAccountant>()
            .LeaveOutstanding("No caller builds its own empty-payload check.");
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            // phase 1: plan, master, verdict
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict)
            // phase 2: its own plan, its own master pass, its own verdict
            .EnqueueText("Planning: move the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// moved"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var progress = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);
        progress.Phases.Should().HaveCount(2);
        progress.Phases.Should().OnlyContain(p => p.State == PhaseRunState.Done,
            "each phase ends at its own VerifyPhase — that is where termination comes from");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "write_file");
    }

    // p0391a, end to end: the harness fixture configures no needs_clarification_status, so
    // the hand-back CANNOT park — and a run that cannot park must fail loudly rather than
    // hand back silently and leave the ticket claimable for the next poll cycle.
    [Fact]
    public async Task Harness_DerivationHandsBackWhereNoParkStatusExists_FailsInsteadOfSilently()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient.EnqueueText(SpecDerivationFixture.NotImplementableJson);

        var runner = new PipelineRunner(harness.Services);
        var result = await runner.RunAsync("code");

        runner.LastContext!.Get<SpecHandback>(ContextKeys.SpecHandback)
            .Case.Should().Be(SpecHandbackCase.NotImplementable);
        harness.ChatClient.ToolCalls.Should().BeEmpty(
            "an unimplementable ticket stops instead of producing a plausible wrong spec");
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("needs_clarification_status");
    }
}
