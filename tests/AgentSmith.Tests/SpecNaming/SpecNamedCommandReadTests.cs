using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using FluentAssertions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa/967ab: a stored step, planned step, checkpoint or operator preset naming a
/// spec-block command by the name it had before the rename is labelled, classified and placed on
/// the storybar as the command this release declares — renamed, not retired.
/// </summary>
public sealed class SpecNamedCommandReadTests
{
    public static TheoryData<string, string> Renamed => new()
    {
        { "WritePhaseRecordCommand", CommandNames.WriteSpecRecord },
        { "VerifyPhaseCommand", CommandNames.VerifySpec },
        { "CommitPhaseWorkCommand", CommandNames.CommitSpecWork },
        { "CheckPhasePremisesCommand", CommandNames.CheckSpecPremises },
        { "ReviewPhaseDiffCommand", CommandNames.ReviewSpecDiff },
        { "PhaseSequenceCommand", CommandNames.SpecSequence },
        { "SelectPhaseCommand", CommandNames.SelectSpec },
        { "PhaseSpecGateCommand", CommandNames.SpecGate },
    };

    [Theory]
    [MemberData(nameof(Renamed))]
    public void StoredCommandName_OldName_ReadsAsTheDeclaredCommand(string accepted, string declared)
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
        CommandNames.Declared("VerifyPhaseCommand:api").Should().Be(CommandNames.VerifySpec + ":api");
        CommandNames.Declared(CommandNames.AgenticMaster).Should().Be(CommandNames.AgenticMaster);
        CommandNames.Declared(CommandNames.RunVerifyPhase).Should().Be(CommandNames.RunVerifyPhase,
            "the execution-stage verify keeps its name");
    }

    [Fact]
    public void GateSilence_OldGateName_KnowsItsNoOpSentence()
    {
        GateSilence.IsNoOpSummary("PhaseSpecGateCommand", "x").Should()
            .Be(GateSilence.IsNoOpSummary(CommandNames.SpecGate, "x"));
    }
}
