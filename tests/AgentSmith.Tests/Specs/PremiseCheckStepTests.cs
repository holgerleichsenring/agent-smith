using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79c: a command is not only a handler. The step's place in the spec block and
/// its five table entries are what make it a step a run can execute, display and account for.
/// </summary>
public sealed class PremiseCheckStepTests
{
    [Fact]
    public void CodeSpecBlock_PremiseCheckFollowsSelectSpecAndPrecedesTheMaster()
    {
        var block = PipelinePresets.CodeSpecBlock.ToList();

        block.IndexOf(CommandNames.CheckSpecPremises).Should().Be(
            block.IndexOf(CommandNames.SelectSpec) + 1,
            "the phase must be current before its premises can be read");
        block.IndexOf(CommandNames.CheckSpecPremises).Should().BeLessThan(
            block.IndexOf(CommandNames.AgenticMaster),
            "the point is to stop before a master token is spent");
    }

    [Fact]
    public void PremiseCheck_SpecAlreadySatisfiedOnEntry_IsDroppedWithTheWorkSteps()
    {
        // SelectSpec drops SpecWorkSteps when the branch already satisfies the spec. A
        // spec that needs no work needs no premise check, and gets none by membership.
        PipelinePresets.SpecWorkSteps.Should().Contain(CommandNames.CheckSpecPremises);
        PipelinePresets.SpecWorkSteps.Should().NotContain(CommandNames.SelectSpec);
    }

    [Fact]
    public void PremiseCheck_Beat_IsNotAheadOfTheStepsItPrecedes()
    {
        // The rail is ordered; a beat that jumps forward and back reads as a stage finishing
        // twice. SelectSpec plans, the master builds, and this sits between them.
        CommandBeats.TryGet(CommandNames.SelectSpec, out var select).Should().BeTrue();
        CommandBeats.TryGet(CommandNames.CheckSpecPremises, out var check).Should().BeTrue();
        CommandBeats.TryGet(CommandNames.AgenticMaster, out var master).Should().BeTrue();

        ((int)check).Should().BeGreaterThanOrEqualTo((int)select);
        ((int)check).Should().BeLessThanOrEqualTo((int)master);
    }

    [Fact]
    public void CheckSpecPremises_HasAStepClassBeatModelUseDisplayNameAndProgressLabel()
    {
        CommandStepClasses.All.Should().ContainKey(CommandNames.CheckSpecPremises);
        CommandBeats.TryGet(CommandNames.CheckSpecPremises, out var beat).Should().BeTrue();
        beat.Should().Be(RunBeat.Plan,
            "it runs between SelectSpec and the master and decides whether the phase is built "
            + "at all — a Verify beat would light the rail's last beat before Building starts, "
            + "and a hand-back would read as a failed build");
        var model = CommandModelUse.For(CommandNames.CheckSpecPremises);
        model.Use.Should().Be(ModelUse.Call, "a fresh instance is asked, once per phase");
        model.Answer.Should().NotBeNullOrWhiteSpace();
        CommandDisplayNames.Get(CommandNames.CheckSpecPremises)
            .Should().Be("Check the phase's premises");
        CommandNames.GetLabel(CommandNames.CheckSpecPremises)
            .Should().Be("Checking what the phase rests on");
    }
}
