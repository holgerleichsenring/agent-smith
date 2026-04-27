using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class PipelineExecutorBatchingTests
{
    [Fact]
    public void PeelBatch_MaxConcurrentOne_ReturnsSingleCommand()
    {
        var list = LinkedListOf(
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "alpha", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "beta", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "gamma", 1));

        var batch = PipelineExecutor.PeelBatch(list.First!, maxConcurrent: 1);

        batch.Should().HaveCount(1);
        batch[0].Value.SkillName.Should().Be("alpha");
    }

    [Fact]
    public void PeelBatch_SameRoundContiguous_PicksAll()
    {
        var list = LinkedListOf(
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "alpha", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "beta", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "gamma", 1));

        var batch = PipelineExecutor.PeelBatch(list.First!, maxConcurrent: 4);

        batch.Should().HaveCount(3);
        batch.Select(n => n.Value.SkillName).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void PeelBatch_DifferentRound_StopsAtBoundary()
    {
        var list = LinkedListOf(
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "alpha", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "beta", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "gate", 2));

        var batch = PipelineExecutor.PeelBatch(list.First!, maxConcurrent: 4);

        batch.Should().HaveCount(2);
        batch.Select(n => n.Value.SkillName).Should().Equal("alpha", "beta");
    }

    [Fact]
    public void PeelBatch_DifferentCommandNames_StopsAtBoundary()
    {
        var list = LinkedListOf(
            PipelineCommand.SkillRound(CommandNames.SkillRound, "alpha", 1),
            PipelineCommand.SkillRound(CommandNames.ApiSecuritySkillRound, "beta", 1));

        var batch = PipelineExecutor.PeelBatch(list.First!, maxConcurrent: 4);

        batch.Should().HaveCount(1);
        batch[0].Value.SkillName.Should().Be("alpha");
    }

    [Fact]
    public void PeelBatch_NonBatchableCommand_ReturnsSingle()
    {
        var list = LinkedListOf(
            PipelineCommand.Simple(CommandNames.ConvergenceCheck),
            PipelineCommand.Simple(CommandNames.ConvergenceCheck));

        var batch = PipelineExecutor.PeelBatch(list.First!, maxConcurrent: 4);

        batch.Should().HaveCount(1);
    }

    [Fact]
    public void PeelBatch_AllThreeSkillRoundFlavors_AreBatchable()
    {
        foreach (var name in new[]
                 {
                     CommandNames.SkillRound,
                     CommandNames.SecuritySkillRound,
                     CommandNames.ApiSecuritySkillRound
                 })
        {
            var list = LinkedListOf(
                PipelineCommand.SkillRound(name, "a", 1),
                PipelineCommand.SkillRound(name, "b", 1));
            var batch = PipelineExecutor.PeelBatch(list.First!, maxConcurrent: 2);
            batch.Should().HaveCount(2, $"{name} should be batchable");
        }
    }

    [Fact]
    public void ApplyBufferToContext_BatchOrderRespected_DiscussionOrderMatchesGraphOrder()
    {
        var pipeline = new PipelineContext();

        // Simulate: skills A, B, C run in parallel; tasks complete in order C, A, B
        // (i.e. deferred buffers populated in completion order).
        // Apply must respect graph order (A, B, C) regardless of completion order.
        var bufferA = new SkillRoundBuffer("A", 1, [],
            new DiscussionEntry("A", "A-disp", "🅰", 1, "from A"), null);
        var bufferB = new SkillRoundBuffer("B", 1, [],
            new DiscussionEntry("B", "B-disp", "🅱", 1, "from B"), null);
        var bufferC = new SkillRoundBuffer("C", 1, [],
            new DiscussionEntry("C", "C-disp", "©", 1, "from C"), null);

        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, bufferA);
        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, bufferB);
        SkillRoundHandlerBase.ApplyBufferToContext(pipeline, bufferC);

        pipeline.TryGet<List<DiscussionEntry>>(ContextKeys.DiscussionLog, out var log);
        log!.Select(e => e.RoleName).Should().Equal("A", "B", "C");
    }

    private static LinkedList<PipelineCommand> LinkedListOf(params PipelineCommand[] commands)
        => new(commands);
}
