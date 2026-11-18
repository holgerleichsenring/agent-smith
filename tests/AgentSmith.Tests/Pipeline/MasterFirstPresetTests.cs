using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>2026-11-18-9c1cb: nothing judges a spec before its master — no spec review step, no premise check.</summary>
public sealed class MasterFirstPresetTests
{
    [Fact]
    public void CodeSpecBlock_GoesFromSpecSelectionStraightToTheMaster()
    {
        var block = PipelinePresets.CodeSpecBlock.ToList();

        block.IndexOf(CommandNames.AgenticMaster).Should().Be(block.IndexOf(CommandNames.SelectSpec) + 1);
        block.Should().NotContain(CommandNames.CheckSpecPremises);
    }

    [Theory]
    [InlineData(PipelinePresets.CodeName)]
    [InlineData(PipelinePresets.SpecDeriveName)]
    public void Preset_HasNoSpecReviewStep(string preset) =>
        PipelinePresets.Effective(preset).Should().NotContain(CommandNames.ReviewSpecs)
            .And.NotContain(CommandNames.CheckSpecPremises);
}
