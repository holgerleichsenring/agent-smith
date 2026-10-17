using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-17-a196c, through the REAL composition: a master whose verdict claims a criterion met by a search
/// it never ran is re-engaged ONCE in the same step, told which citation resolves against nothing; a second
/// verdict citing the same thing is left to the keystone. Run f381's master reported 7 of 7 met three times
/// over on bare sentences before anything checked a claim.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AcceptanceClaimObjectionTests
{
    private const string UnrunCitation =
        """Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"fixed","acceptance":[{"criterion":"An empty request body is answered with 400.","status":"met","evidence":"no bus remains","citations":["UsingSampleBus|UseSampleBus"]},{"criterion":"Existing callers stay unaffected.","status":"not_applicable","evidence":"no caller sends an empty body"}]}""";

    [Fact]
    public async Task Harness_MetClaimCitingAnUnrunSearch_ReengagesOnceWithTheCitation()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            .EnqueueText(SpecDerivationFixture.DerivationJson)
            .EnqueueText("Planning: I will patch the file.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Patch.cs","content":"// real fix"}""")
            .EnqueueText(UnrunCitation)
            .EnqueueText(UnrunCitation);

        var runner = new PipelineRunner(harness.Services);
        await runner.RunAsync("code");

        var objections = harness.ChatClient.PromptsSeen
            .Where(p => p.Contains("resolves against nothing this spec changed or ran", StringComparison.Ordinal)).ToList();
        objections.Should().NotBeEmpty("the met claim cites a search that never ran")
            .And.OnlyContain(p => p.Contains("An empty request body is answered with 400.: claimed satisfied by 'UsingSampleBus|UseSampleBus'"));
        objections.Select(p => p.Split("resolves against nothing this spec changed or ran").Length - 1).Max()
            .Should().Be(1, "one objection per step, then the keystone judges");
    }
}
