using AgentSmith.Application.Services.Scans;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0422: the framework's own calls — the cut review before the work, the delivery
/// account after it — must never dequeue a preset's script. A preset describes the
/// MASTER; one stolen response shifts the whole sequence and the master silently does
/// the next thing instead of the intended one.
/// </summary>
public sealed class FrameworkCallInterceptionTests
{
    [Fact]
    public void TheCutReviewPrompt_IsRecognisedAsAFrameworkCall()
    {
        var prompt = SpecCutReviewPrompt.For(Set(), "the ticket");

        SpecAccountReply.IsCutReviewCall(prompt).Should().BeTrue(
            "the harness recognises it by its own text — a marker that drifts steals a response");
    }

    [Fact]
    public void TheAccountingPrompt_IsRecognisedAsAFrameworkCall()
    {
        var prompt = SpecAccountPrompt.For(["a criterion"], "diff --git a/x b/x\n", []);

        SpecAccountReply.IsAccountingCall(prompt).Should().BeTrue();
    }

    /// <summary>
    /// p0429: the finding refutation is the third such call. It is stood down by TYPE at
    /// the composition boundary rather than by a text marker, so it cannot reach the
    /// script at all — but only for as long as the substitution is actually registered.
    /// </summary>
    [Fact]
    public async Task TheFindingRefuter_IsStoodDownAtTheCompositionBoundary()
    {
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default));

        harness.Services.GetRequiredService<IFindingRefuter>()
            .Should().BeOfType<HarnessFindingRefuter>(
                "a preset's script describes the MASTER; a framework call drawing from it "
                + "shifts the whole sequence by one");
    }

    private static SpecSet Set() =>
        new("harness", [new SpecPhase(
            new PhaseDraft("p1", "goal", "goal: g", []) { Done = ["a criterion"] },
            "slug", "# md", [])], SpecAccounting.Empty, [], SpecSource.Derived);
}
