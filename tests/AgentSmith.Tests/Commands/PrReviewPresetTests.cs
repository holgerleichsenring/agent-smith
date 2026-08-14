using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// p0167a: shape of the pr-review preset. Note the drift from the original
/// spec's step names: FetchPullRequest folded into AnalyzePrDiff (the handler
/// reads IPrDiffProvider itself), BootstrapProject (retired p0131b) replaced
/// by BootstrapCheck + BootstrapGate, AnalyzeProject == AnalyzeCode.
/// p0312c: LoadSkills + Triage + RunReviewPhase gave way to AgenticMaster +
/// MergeMasterFindings — the p0179d shape every other pipeline already had.
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
            CommandNames.LoadCodingPrinciples, CommandNames.LoadMemoryIndex, // p0380
            CommandNames.LoadContext,
            CommandNames.AnalyzeCode,
            CommandNames.AnalyzePrDiff,
            CommandNames.AgenticMaster,
            CommandNames.MergeMasterFindings,
            CommandNames.CompilePrReviewFindings,
            CommandNames.WriteRunResult,
            CommandNames.PostPrComments,
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void PrReviewPreset_AnalyzePrDiff_AfterCheckoutAndBeforeTheMaster()
    {
        // The master reads ContextKeys.PrDiff through {PrDiffSection}, so the
        // parse must land before AgenticMaster or it reviews nothing.
        var preset = PipelinePresets.TryResolve("pr-review")!.ToList();
        var analyzeIdx = preset.IndexOf(CommandNames.AnalyzePrDiff);

        analyzeIdx.Should().BeGreaterThan(preset.IndexOf(CommandNames.CheckoutSource));
        analyzeIdx.Should().BeLessThan(preset.IndexOf(CommandNames.AgenticMaster));
    }

    [Fact]
    public void PrReviewPreset_MergeMasterFindings_BetweenMasterAndCompile()
    {
        // p0312c: the master's observations reach CompilePrReviewFindings the
        // same way security-scan's do — through ContextKeys.SkillObservations.
        var preset = PipelinePresets.TryResolve("pr-review")!.ToList();

        preset.IndexOf(CommandNames.MergeMasterFindings)
            .Should().BeGreaterThan(preset.IndexOf(CommandNames.AgenticMaster));
        preset.IndexOf(CommandNames.MergeMasterFindings)
            .Should().BeLessThan(preset.IndexOf(CommandNames.CompilePrReviewFindings));
    }

    [Fact]
    public void PrReviewPreset_IsRegisteredAsStructuredReadOnlyPipeline()
    {
        PipelinePresets.Names.Should().Contain("pr-review");
        PipelinePresets.GetPipelineType("pr-review").Should().Be(PipelineType.Structured);
        // Review emits comments, not code: a run with zero code changes is a
        // legitimate success and no green-test verdict is required.
        PipelinePresets.ExpectsCodeChanges("pr-review").Should().BeFalse();
        PipelinePresets.ExpectsCodeChanges("pr-review").Should().BeFalse();
    }

    [Fact]
    public void PrReviewPreset_ResolvesSkillsFromTheCatalogRoot()
    {
        // p0312a: one root for every pipeline — the per-category map is gone
        // together with the category directories it pointed at.
        PipelinePresets.GetDefaultSkillsPath("pr-review").Should().Be("skills");
    }
}
