using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Thread;

public sealed class DialogSurfaceLimitsTests
{
    private const string DesignMaster = "design-partner-master";

    [Fact]
    public void DialogSurfaceLimits_DesignTurn_SetsWindowCompactionIterationsAndCapture()
    {
        var (pipeline, slot) = Seeded(new DialogThread(false, [new ChatMessage(ChatRole.User, "turn 1")], []));
        var agent = new AgentConfig();

        var request = DialogSurfaceLimits.Apply(Request(agent), Context(DesignMaster, agent, pipeline));

        request.MaxIterations.Should().Be(100);
        request.Compaction.Should().BeSameAs(agent.Compaction);
        request.ContextWindowTokensOverride.Should().Be(200000);
        request.ThreadCapture.Should().BeSameAs(slot.Thread);
        request.PriorMessages!.Single().Text.Should().Be("turn 1");
    }

    [Fact]
    public void DialogSurfaceLimits_StewardThreadOnDesignTurn_SeedsNoPriorMessages()
    {
        var (pipeline, _) = Seeded(new DialogThread(true, [new ChatMessage(ChatRole.User, "steward turn")], []));
        var agent = new AgentConfig();

        DialogSurfaceLimits.Apply(Request(agent), Context(DesignMaster, agent, pipeline))
            .PriorMessages.Should().BeNull("a turn never replays the other master's thread");
        DialogSurfaceLimits.Apply(Request(agent), Context(PipelinePresets.TicketStewardMaster, agent, pipeline))
            .PriorMessages.Should().ContainSingle();
    }

    [Fact]
    public void MasterConversation_OpenedWithPriorMessages_ThreadStartsWithThem()
    {
        var prior = new ChatMessage(ChatRole.User, "turn 1");
        var conversation = new MasterConversation();

        conversation.Opened(Request(new AgentConfig()) with { PriorMessages = [prior] },
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer")));

        conversation.Thread().Select(m => m.Text).Should().Equal("turn 1", "this turn", "answer");
    }

    private static (PipelineContext Pipeline, SpecDialogReplySlot Slot) Seeded(DialogThread thread)
    {
        var pipeline = new PipelineContext();
        var slot = new SpecDialogReplySlot();
        pipeline.Set(ContextKeys.SpecDialogReplySlot, slot);
        pipeline.Set(ContextKeys.SpecDialogThread, thread);
        return (pipeline, slot);
    }

    private static AgenticLoopRequest Request(AgentConfig agent) =>
        new(agent, TaskType.Primary, "system", "this turn", []);

    private static AgenticMasterContext Context(string master, AgentConfig agent, PipelineContext pipeline) =>
        new(master, new Repository(new BranchName("main"), "https://example.invalid/repo.git"), string.Empty, agent, pipeline);
}
