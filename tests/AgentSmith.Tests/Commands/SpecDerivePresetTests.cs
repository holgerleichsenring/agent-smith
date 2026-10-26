using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-26-bdd0a: the spec derivation is the code preset's front and nothing after it.</summary>
public sealed class SpecDerivePresetTests
{
    [Fact]
    public void SpecDerive_IsTheCodeFrontThroughSpecGate()
    {
        var front = PipelinePresets.Code
            .TakeWhile(c => c != CommandNames.EnsurePrerequisites)
            .Where(c => c != CommandNames.PipelineNameInitializer);

        PipelinePresets.TryResolve(PipelinePresets.SpecDeriveName).Should().Equal(front,
            "the derivation, the review rounds and the placement are the code run's own");
        PipelinePresets.SpecDerive[^1].Should().Be(CommandNames.SpecGate);
    }

    [Fact]
    public void SpecDerive_RunsNoMasterAndShipsNoCode()
    {
        var steps = PipelinePresets.Effective(PipelinePresets.SpecDeriveName);

        steps.Should().NotContain([CommandNames.AgenticMaster, CommandNames.SpecSequence, CommandNames.CommitAndPR,
            CommandNames.WriteRunResult, CommandNames.EnsurePrerequisites]);
        PipelinePresets.ExpectsCodeChanges(PipelinePresets.SpecDeriveName).Should().BeFalse();
        PipelinePresets.GetPipelineType(PipelinePresets.SpecDeriveName).Should().Be(PipelineType.Discussion);
    }
}
