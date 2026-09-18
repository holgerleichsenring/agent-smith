using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79c: a command is not only a handler. The step's place in the phase block and
/// its five table entries are what make it a step a run can execute, display and account for.
/// </summary>
public sealed class PremiseCheckStepTests
{
    [Fact]
    public void CodePhaseBlock_PremiseCheckFollowsSelectPhaseAndPrecedesTheMaster()
    {
        var block = PipelinePresets.CodePhaseBlock.ToList();

        block.IndexOf(CommandNames.CheckPhasePremises).Should().Be(
            block.IndexOf(CommandNames.SelectPhase) + 1,
            "the phase must be current before its premises can be read");
        block.IndexOf(CommandNames.CheckPhasePremises).Should().BeLessThan(
            block.IndexOf(CommandNames.AgenticMaster),
            "the point is to stop before a master token is spent");
    }

    [Fact]
    public void PremiseCheck_PhaseAlreadySatisfiedOnEntry_IsDroppedWithTheWorkSteps()
    {
        // SelectPhase drops PhaseWorkSteps when the branch already satisfies the phase. A
        // phase that needs no work needs no premise check, and gets none by membership.
        PipelinePresets.PhaseWorkSteps.Should().Contain(CommandNames.CheckPhasePremises);
        PipelinePresets.PhaseWorkSteps.Should().NotContain(CommandNames.SelectPhase);
    }

    [Fact]
    public void PremiseCheck_Beat_IsNotAheadOfTheStepsItPrecedes()
    {
        // The rail is ordered; a beat that jumps forward and back reads as a stage finishing
        // twice. SelectPhase plans, the master builds, and this sits between them.
        CommandBeats.TryGet(CommandNames.SelectPhase, out var select).Should().BeTrue();
        CommandBeats.TryGet(CommandNames.CheckPhasePremises, out var check).Should().BeTrue();
        CommandBeats.TryGet(CommandNames.AgenticMaster, out var master).Should().BeTrue();

        ((int)check).Should().BeGreaterThanOrEqualTo((int)select);
        ((int)check).Should().BeLessThanOrEqualTo((int)master);
    }

    [Fact]
    public void CheckPhasePremises_HasAStepClassBeatModelUseDisplayNameAndProgressLabel()
    {
        CommandStepClasses.All.Should().ContainKey(CommandNames.CheckPhasePremises);
        CommandBeats.TryGet(CommandNames.CheckPhasePremises, out var beat).Should().BeTrue();
        beat.Should().Be(RunBeat.Plan,
            "it runs between SelectPhase and the master and decides whether the phase is built "
            + "at all — a Verify beat would light the rail's last beat before Building starts, "
            + "and a hand-back would read as a failed build");
        var model = CommandModelUse.For(CommandNames.CheckPhasePremises);
        model.Use.Should().Be(ModelUse.Call, "a fresh instance is asked, once per phase");
        model.Answer.Should().NotBeNullOrWhiteSpace();
        CommandDisplayNames.Get(CommandNames.CheckPhasePremises)
            .Should().Be("Check the phase's premises");
        CommandNames.GetLabel(CommandNames.CheckPhasePremises)
            .Should().Be("Checking what the phase rests on");
    }
}
