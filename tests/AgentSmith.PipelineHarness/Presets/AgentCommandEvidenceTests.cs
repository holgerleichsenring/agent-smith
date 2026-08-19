using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0469 through the REAL composition: a phase that completes normally hands the delivery
/// account the commands the agent ran in it.
/// <para>
/// p0452 published them from the budget catch, the general catch and the mid-run-question
/// park — never from the path a phase takes when it works. Every live run that finished
/// therefore handed the reader only the verification stage's own build and test lines, and
/// one was refused for an exhaustive scan the agent had run several times. A handler test
/// can prove the publication; only the wired pipeline proves it still reaches the reader
/// at the other end of a run.
/// </para>
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AgentCommandEvidenceTests
{
    private const string GreenVerdict =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed"}""";

    [Fact]
    public async Task RealComposition_MasterRanASearch_TheVerificationIsShownIt()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will check nothing references the legacy library.")
            .EnqueueToolCall("run_command", """{"command":"grep -rn Legacy src","repo":"primary"}""")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(GreenVerdict);

        await new PipelineRunner(harness.Services).RunAsync("code");

        harness.Services.GetRequiredService<HarnessSpecAccountant>().CommandResultsShown
            .Should().Contain(line => line.Contains("the agent ran 'grep -rn Legacy src'"),
                "the reader judging the phase is shown what the agent ran in it");
    }
}
