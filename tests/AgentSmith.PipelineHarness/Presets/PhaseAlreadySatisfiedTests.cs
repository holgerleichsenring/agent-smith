using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0460, through the REAL composition: a phase whose ratified criteria the branch already
/// satisfies is recorded as done and the sequence moves on — no master pass, no cost, no
/// question put to the operator.
/// <para>
/// This is the shape a re-triggered run has. A run parks or dies mid-sequence with its work
/// committed on the ticket branch; the next run derives the same phases and, until now,
/// opened the first one as if nothing had happened.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PhaseAlreadySatisfiedTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled"}]}""";

    [Fact]
    public async Task Harness_APhaseTheBranchAlreadySatisfies_IsNotWorkedAgain()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        // Only phase 1 is scripted. Phase 2 is entered over the branch phase 1 delivered,
        // and the entry account finds it satisfied — a second master pass would have to
        // draw from an empty script.
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.TwoPhaseJson)
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var progress = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);
        var second = progress.Phases[1];
        second.State.Should().Be(PhaseRunState.Done);
        second.Note.Should().Be(SelectPhaseHandler.AlreadySatisfiedNote,
            "only the entry account writes this note — the second phase was REACHED and "
            + "accounted for, not silently dropped");
        harness.ChatClient.ToolCalls.Should().ContainSingle(
            "one phase did work; the other was already delivered");
    }

    /// <summary>
    /// The common case must stay free. On a fresh branch the first phase has an empty diff,
    /// where an account can only say "nothing is satisfied" — so none is taken, and the
    /// phase runs exactly as it did before.
    /// </summary>
    [Fact]
    public async Task Harness_TheFirstPhaseOfAFreshBranch_IsWorkedWithoutAnAccount()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(GreenVerdict);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var progress = runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress);
        progress.Phases.Should().ContainSingle();
        progress.Phases[0].Note.Should().BeNull("nothing on the branch could have satisfied it");
        harness.ChatClient.ToolCalls.Should().ContainSingle("the phase was worked");
    }
}
