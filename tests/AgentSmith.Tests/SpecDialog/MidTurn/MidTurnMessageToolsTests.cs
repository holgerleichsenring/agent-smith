using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.SpecDialog.MidTurn;

/// <summary>2026-11-12-a1fa: only a seeded design turn's own functions hand on mid-turn messages.</summary>
public sealed class MidTurnMessageToolsTests
{
    private readonly IList<AITool> _tools = [AIFunctionFactory.Create(() => "file text", "read_file")];

    [Fact]
    public void Delivering_NothingSeeded_ReturnsTheSurfaceUnchanged()
    {
        new MidTurnMessageTools().Delivering(_tools, Context("spec-dialog-master", new PipelineContext()))
            .Should().BeSameAs(_tools);
    }

    [Fact]
    public void Delivering_DesignTurn_WrapsEachFunctionAndLeadsAsDesign()
    {
        var messages = new Mock<IMidTurnMessages>();

        var tools = new MidTurnMessageTools().Delivering(_tools, Context("spec-dialog-master", Seeded(messages.Object)));

        tools.Should().ContainSingle().Which.Should().BeOfType<MidTurnMessageAIFunction>().Which.Name.Should().Be("read_file");
        messages.Verify(m => m.Lead(false), Times.Once);
    }

    [Fact]
    public void Delivering_TicketSteward_LeadsAsStewardAndWrapsNothing()
    {
        var messages = new Mock<IMidTurnMessages>();

        var tools = new MidTurnMessageTools().Delivering(
            _tools, Context(PipelinePresets.TicketStewardMaster, Seeded(messages.Object)));

        tools.Should().BeSameAs(_tools);
        messages.Verify(m => m.Lead(true), Times.Once);
    }

    [Fact]
    public async Task MidTurnMessageAIFunction_NothingWaiting_ReturnsTheResultUntouched()
    {
        var messages = new Mock<IMidTurnMessages>();
        messages.Setup(m => m.Drain()).Returns([]);
        var inner = AIFunctionFactory.Create(() => "file text", "read_file");

        var result = await new MidTurnMessageAIFunction(inner, messages.Object).InvokeAsync(new AIFunctionArguments());

        result!.ToString().Should().Be("file text");
    }

    private static PipelineContext Seeded(IMidTurnMessages messages)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogOperatorMessages, messages);
        return pipeline;
    }

    private static AgenticMasterContext Context(string master, PipelineContext pipeline) =>
        new(master, new Repository(new BranchName("main"), "https://example.invalid/repo.git"), string.Empty,
            new AgentConfig(), pipeline);
}
