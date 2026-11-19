using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-10-26-bdd0a: the spec derivation is the code preset's front; 2026-11-19-6b0ba: then the coding master writes each
/// stand-in and builds nothing — no prerequisites, no commit, no pull request of code.
/// </summary>
public sealed class SpecDerivePresetTests
{
    [Fact]
    public void SpecDerive_IsTheCodeFrontThroughSpecGateThenTheSequence()
    {
        var front = PipelinePresets.Code
            .TakeWhile(c => c != CommandNames.EnsurePrerequisites)
            .Where(c => c != CommandNames.PipelineNameInitializer);

        PipelinePresets.TryResolve(PipelinePresets.SpecDeriveName).Should().Equal(
            [.. front, CommandNames.MaterializeReferenceSets, CommandNames.SpecSequence],
            "the stand-ins and the placement are the code run's own");
    }

    [Fact]
    public void SpecDerive_RunsTheCodingMasterOnEachStandInAndShipsNoCode()
    {
        var steps = PipelinePresets.Effective(PipelinePresets.SpecDeriveName);

        steps.TakeLast(3).Should().Equal(CommandNames.SelectSpec, CommandNames.AgenticMaster, CommandNames.SpecGate);
        steps.Should().NotContain([CommandNames.CommitSpecWork, CommandNames.VerifySpec, CommandNames.CommitAndPR,
            CommandNames.WriteRunResult, CommandNames.EnsurePrerequisites, CommandNames.MasterOpenQuestions]);
        PipelinePresets.MasterFor(PipelinePresets.SpecDeriveName).Should().Be(PipelinePresets.CodingMaster);
        PipelinePresets.ExpectsCodeChanges(PipelinePresets.SpecDeriveName).Should().BeFalse();
        PipelinePresets.GetPipelineType(PipelinePresets.SpecDeriveName).Should().Be(PipelineType.Discussion);
    }
}
