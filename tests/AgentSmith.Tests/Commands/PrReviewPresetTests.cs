using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0167a: shape of the pr-review preset. Note the drift from the original
/// spec's step names: FetchPullRequest folded into AnalyzePrDiff (the handler
/// reads IPrDiffProvider itself), BootstrapProject (retired p0131b) replaced
/// by BootstrapCheck + BootstrapGate, AnalyzeProject == AnalyzeCode.
/// </summary>
public sealed class PrReviewPresetTests
{
    [Fact]
    public void PrReviewPreset_StepSequence_ContainsCheckoutThroughPostComments()
    {
        PipelinePresets.TryResolve("pr-review").Should().BeEquivalentTo(
        [
            CommandNames.LoadCatalog,
            CommandNames.PipelineNameInitializer,
            CommandNames.CheckoutSource,
            CommandNames.BootstrapCheck, CommandNames.BootstrapGate,
            CommandNames.LoadCodingPrinciples, CommandNames.LoadContext,
            CommandNames.AnalyzeCode,
            CommandNames.AnalyzePrDiff,
            CommandNames.LoadSkills,
            CommandNames.Triage,
            CommandNames.RunReviewPhase,
            CommandNames.CompilePrReviewFindings,
            CommandNames.WriteRunResult,
            CommandNames.PostPrComments,
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void PrReviewPreset_AnalyzePrDiff_AfterCheckoutAndBeforeTriage()
    {
        // The review skills consume ContextKeys.PrDiff, so the parse must land
        // before the Triage -> RunReviewPhase dispatch.
        var preset = PipelinePresets.TryResolve("pr-review")!.ToList();
        var analyzeIdx = preset.IndexOf(CommandNames.AnalyzePrDiff);

        analyzeIdx.Should().BeGreaterThan(preset.IndexOf(CommandNames.CheckoutSource));
        analyzeIdx.Should().BeLessThan(preset.IndexOf(CommandNames.Triage));
    }

    [Fact]
    public void PrReviewPreset_IsRegisteredAsStructuredReadOnlyPipeline()
    {
        PipelinePresets.Names.Should().Contain("pr-review");
        PipelinePresets.GetPipelineType("pr-review").Should().Be(PipelineType.Structured);
        // Review emits comments, not code: a run with zero code changes is a
        // legitimate success and no green-test verdict is required.
        PipelinePresets.ExpectsCodeChanges("pr-review").Should().BeFalse();
        PipelinePresets.ExpectsGreenTests("pr-review").Should().BeFalse();
    }

    [Fact]
    public void PrReviewPreset_IsMultiPhase_SoReviewSkillAssignmentsAreNotCollapsed()
    {
        // p0167b's review skills declare phase: review; StructuredTriageStrategy
        // must NOT collapse them into the Plan phase.
        PipelinePresets.IsSinglePhase("pr-review").Should().BeFalse();
    }

    [Fact]
    public void PrReviewPreset_DefaultSkillsPath_IsPrReviewRoster()
    {
        PipelinePresets.GetDefaultSkillsPath("pr-review").Should().Be("skills/pr-review");
    }
}
