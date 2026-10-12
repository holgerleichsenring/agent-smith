using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa: a stored step, planned step or checkpoint naming a spec-block command by the
/// name a later release writes is labelled, classified and placed on the storybar as the command
/// this release declares — renamed, not retired.
/// </summary>
public sealed class SpecNamedCommandReadTests
{
    public static TheoryData<string, string> Renamed => new()
    {
        { "WriteSpecRecordCommand", CommandNames.WritePhaseRecord },
        { "VerifySpecCommand", CommandNames.VerifyPhase },
        { "CommitSpecWorkCommand", CommandNames.CommitPhaseWork },
        { "CheckSpecPremisesCommand", CommandNames.CheckPhasePremises },
        { "ReviewSpecDiffCommand", CommandNames.ReviewPhaseDiff },
        { "SpecSequenceCommand", CommandNames.PhaseSequence },
        { "SelectSpecCommand", CommandNames.SelectPhase },
        { "SpecGateCommand", CommandNames.PhaseSpecGate },
    };

    [Theory]
    [MemberData(nameof(Renamed))]
    public void StoredCommandName_SpecNamed_ReadsAsTheDeclaredCommand(string accepted, string declared)
    {
        CommandNames.Declared(accepted).Should().Be(declared);
        CommandDisplayNames.Get(accepted).Should().Be(CommandDisplayNames.Get(declared));
        CommandStepClasses.Get(accepted).Should().Be(CommandStepClasses.Get(declared));
        CommandNames.GetLabel(accepted).Should().Be(CommandNames.GetLabel(declared));
        CommandModelUse.For(accepted).Should().Be(CommandModelUse.For(declared));
        CommandBeats.TryGet(accepted, out var beat).Should().Be(CommandBeats.TryGet(declared, out var declaredBeat));
        beat.Should().Be(declaredBeat);
        CommandNames.RetiredCommands.Should().NotContainKey(accepted, "a renamed command is not retired");
    }

    [Fact]
    public void Declared_KeepsAParameterSuffix_AndLeavesOtherNamesAlone()
    {
        CommandNames.Declared("VerifySpecCommand:api").Should().Be(CommandNames.VerifyPhase + ":api");
        CommandNames.Declared(CommandNames.AgenticMaster).Should().Be(CommandNames.AgenticMaster);
        CommandNames.Declared(CommandNames.RunVerifyPhase).Should().Be(CommandNames.RunVerifyPhase,
            "the execution-stage verify keeps its name");
    }

    [Fact]
    public void GateSilence_SpecNamedGate_KnowsItsNoOpSentence()
    {
        GateSilence.IsNoOpSummary("SpecGateCommand", "x").Should()
            .Be(GateSilence.IsNoOpSummary(CommandNames.PhaseSpecGate, "x"));
    }
}
