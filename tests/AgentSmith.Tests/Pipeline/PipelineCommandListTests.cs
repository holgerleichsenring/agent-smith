using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0460: a step may retire the steps its own phase no longer needs — and nothing beyond
/// them. Eating the next phase's master would turn "this phase is already done" into
/// "the rest of the migration is already done", which is the one failure mode of skipping.
/// </summary>
public sealed class PipelineCommandListTests
{
    [Fact]
    public void ARetiredStep_LeavesTheList()
    {
        var (commands, first) = Block();

        List().DropAhead(first, commands, Drop());

        commands.Select(c => c.Name).Should().Equal(
            CommandNames.SelectPhase, CommandNames.WritePhaseRecord,
            CommandNames.SelectPhase, CommandNames.AgenticMaster, CommandNames.WritePhaseRecord);
    }

    [Fact]
    public void ARetiredStep_NeverCrossesThePhaseBoundary()
    {
        var (commands, first) = Block();

        List().DropAhead(first, commands, Drop());

        // The second phase has not been accounted for and keeps every step it was given.
        commands.Where(c => c.PhaseId == "p2").Select(c => c.Name).Should().Equal(
            [CommandNames.SelectPhase, CommandNames.AgenticMaster, CommandNames.WritePhaseRecord]);
    }

    [Fact]
    public void AStepBelongingToNoPhase_RetiresNothing()
    {
        var commands = new LinkedList<PipelineCommand>(
        [
            new PipelineCommand(CommandNames.SelectPhase),
            new PipelineCommand(CommandNames.AgenticMaster),
        ]);

        List().DropAhead(commands.First!, commands, Drop());

        commands.Should().HaveCount(2, "a step outside any phase has no phase to retire");
    }

    private static PipelineCommandList List() =>
        new(NullLogger<PipelineCommandList>.Instance);

    private static CommandResult Drop() =>
        CommandResult.OkAndDropAhead("already satisfied", [CommandNames.AgenticMaster]);

    private static (LinkedList<PipelineCommand> Commands, LinkedListNode<PipelineCommand> First) Block()
    {
        var commands = new LinkedList<PipelineCommand>(
        [
            .. Phase("p1"),
            .. Phase("p2"),
        ]);
        return (commands, commands.First!);
    }

    private static IEnumerable<PipelineCommand> Phase(string phaseId) =>
        new[] { CommandNames.SelectPhase, CommandNames.AgenticMaster, CommandNames.WritePhaseRecord }
            .Select(name => new PipelineCommand(name) { PhaseId = phaseId });
}
