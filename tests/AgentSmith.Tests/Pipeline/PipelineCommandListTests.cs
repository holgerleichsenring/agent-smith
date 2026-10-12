using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pipeline;

/// <summary>
/// p0460: a step may retire the steps its own spec no longer needs — and nothing beyond
/// them. Eating the next spec's master would turn "this spec is already done" into
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
            CommandNames.SelectSpec, CommandNames.WriteSpecRecord,
            CommandNames.SelectSpec, CommandNames.AgenticMaster, CommandNames.WriteSpecRecord);
    }

    [Fact]
    public void ARetiredStep_NeverCrossesTheSpecBoundary()
    {
        var (commands, first) = Block();

        List().DropAhead(first, commands, Drop());

        // The second spec has not been accounted for and keeps every step it was given.
        commands.Where(c => c.SpecId == "p2").Select(c => c.Name).Should().Equal(
            [CommandNames.SelectSpec, CommandNames.AgenticMaster, CommandNames.WriteSpecRecord]);
    }

    [Fact]
    public void AStepBelongingToNoSpec_RetiresNothing()
    {
        var commands = new LinkedList<PipelineCommand>(
        [
            new PipelineCommand(CommandNames.SelectSpec),
            new PipelineCommand(CommandNames.AgenticMaster),
        ]);

        List().DropAhead(commands.First!, commands, Drop());

        commands.Should().HaveCount(2, "a step outside any spec has no spec to retire");
    }

    private static PipelineCommandList List() =>
        new(NullLogger<PipelineCommandList>.Instance);

    private static CommandResult Drop() =>
        CommandResult.OkAndDropAhead("already satisfied", [CommandNames.AgenticMaster]);

    private static (LinkedList<PipelineCommand> Commands, LinkedListNode<PipelineCommand> First) Block()
    {
        var commands = new LinkedList<PipelineCommand>(
        [
            .. Spec("p1"),
            .. Spec("p2"),
        ]);
        return (commands, commands.First!);
    }

    private static IEnumerable<PipelineCommand> Spec(string specId) =>
        new[] { CommandNames.SelectSpec, CommandNames.AgenticMaster, CommandNames.WriteSpecRecord }
            .Select(name => new PipelineCommand(name) { SpecId = specId });
}
