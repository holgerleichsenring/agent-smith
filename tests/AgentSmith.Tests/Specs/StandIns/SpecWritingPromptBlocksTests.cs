using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.StandIns;

/// <summary>2026-11-19-6b0ba: the coding master is told to write the spec only when it is on a stand-in, told the
/// input over a kept spec, and given the write-only contract only in a spec derivation.</summary>
public sealed class SpecWritingPromptBlocksTests
{
    [Fact]
    public void StandIn_OnAStandIn_SaysToWriteTheSpecFirstWithReviseSpec()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft);

        SpecWritingPromptBlocks.StandIn(pipeline, StandInSets.Draft)
            .Should().Contain("## Write the spec first").And.Contain("revise_spec").And.Contain(StandInSets.Draft.SpecId);
    }

    [Fact]
    public void StandIn_OnAWrittenSpec_SaysNothing()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft, standIn: false);

        SpecWritingPromptBlocks.StandIn(pipeline, StandInSets.Draft).Should().BeEmpty();
    }

    [Fact]
    public void KeptInput_OverAWrittenSpec_NamesTheCause()
    {
        var pipeline = StandInSets.Run(new PipelineContext(), StandInSets.Draft, standIn: false);
        pipeline.Set(ContextKeys.SpecKeptInput, SpecRevisionCauseText.Comment);

        SpecWritingPromptBlocks.KeptInput(pipeline, StandInSets.Draft)
            .Should().Contain(SpecRevisionCauseText.Comment).And.Contain("revise_spec");
    }

    [Fact]
    public void WriteOnly_UnderSpecDerive_IsTheContract()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDeriveName);

        SpecWritingPromptBlocks.IsWriteOnly(pipeline).Should().BeTrue();
        SpecWritingPromptBlocks.WriteOnlyContract(StandInSets.Draft)
            .Should().Contain("builds nothing").And.Contain("status blocked").And.NotContain("ask_human");
    }

    [Fact]
    public void WriteOnly_UnderCode_IsNot()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.CodeName);

        SpecWritingPromptBlocks.IsWriteOnly(pipeline).Should().BeFalse();
    }

    private static class SpecRevisionCauseText
    {
        public const string Comment = AgentSmith.Application.Services.Specs.SpecRevisionCause.Comment;
    }
}
