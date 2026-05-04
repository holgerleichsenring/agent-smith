using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class PhaseCommandExpanderTests
{
    private readonly PhaseCommandExpander _sut = new();

    [Fact]
    public void ExpandPhase_PlanWithLeadAndAnalysts_EmitsSkillRoundCommandsInOrder()
    {
        var triage = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect", new[] { "tester" }, Array.Empty<string>(), null)
            }, 85, string.Empty);

        var commands = _sut.ExpandPhase(triage, PipelinePhase.Plan, round: 1, CommandNames.SkillRound);

        commands.Should().HaveCount(2);
        commands[0].SkillName.Should().Be("architect");
        commands[0].Round.Should().Be(1);
        commands[1].SkillName.Should().Be("tester");
    }

    [Fact]
    public void ExpandPhase_FinalWithFilter_EmitsFilterRoundCommand()
    {
        var triage = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Final] = new(null, Array.Empty<string>(), Array.Empty<string>(), "reducer")
            }, 85, string.Empty);

        var commands = _sut.ExpandPhase(triage, PipelinePhase.Final, round: 3, CommandNames.SkillRound);

        commands.Should().HaveCount(1);
        commands[0].Name.Should().Be(CommandNames.FilterRound);
        commands[0].SkillName.Should().Be("reducer");
    }

    [Fact]
    public void ExpandPhase_PhaseNotInTriage_ReturnsEmptyList()
    {
        var triage = new TriageOutput(new Dictionary<PipelinePhase, PhaseAssignment>(), 85, string.Empty);

        var commands = _sut.ExpandPhase(triage, PipelinePhase.Review, round: 2, CommandNames.SkillRound);

        commands.Should().BeEmpty();
    }

    [Fact]
    public void ExpandPhase_SecurityPipelineCommandName_PassesThrough()
    {
        var triage = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect", Array.Empty<string>(), Array.Empty<string>(), null)
            }, 85, string.Empty);

        var commands = _sut.ExpandPhase(triage, PipelinePhase.Plan, round: 1, CommandNames.SecuritySkillRound);

        commands[0].Name.Should().Be(CommandNames.SecuritySkillRound);
    }
}
