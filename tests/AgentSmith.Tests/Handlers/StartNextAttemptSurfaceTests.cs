using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-14-da5ed: start_next_attempt joins the design master only when its bound turn was seeded with the
/// start port — a poster with runs.control on a worked ticket — and never a child's surface.
/// </summary>
public sealed class StartNextAttemptSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string Tool = "start_next_attempt";

    [Fact]
    public async Task DesignTurn_SeededStartPort_CarriesStartNextAttempt()
    {
        var loop = new SurfaceCapturingLoop();
        var context = DesignContext();
        context.Pipeline.Set(ContextKeys.SpecDialogAttemptStart, Mock.Of<IAttemptStart>());

        await Run(loop, context);

        loop.MasterTools.Should().Contain(Tool);
    }

    [Fact]
    public async Task AttemptStart_PosterWithoutRunsControl_ToolAbsent()
    {
        var loop = new SurfaceCapturingLoop();

        await Run(loop, DesignContext()); // no port seeded: no right, or a ticket never worked

        loop.MasterTools.Should().NotContain(Tool);
    }

    [Fact]
    public async Task SpecDialogSubAgent_ChildTools_DoNotIncludeStartNextAttempt()
    {
        var loop = new SurfaceCapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        var context = DesignContext();
        context.Pipeline.Set(ContextKeys.SpecDialogAttemptStart, Mock.Of<IAttemptStart>());

        await Run(loop, context, children);

        loop.MasterTools.Should().Contain(Tool);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should()
            .NotContain(Tool, "a child acts on no person's word");
    }

    [Fact]
    public async Task StartNextAttempt_Requested_SaysRequested()
    {
        var port = new Mock<IAttemptStart>();
        port.Setup(p => p.StartNextAsync("the registry is added", It.IsAny<CancellationToken>()))
            .ReturnsAsync("requested — the next attempt starts through the rework queue");
        var tool = new StartNextAttemptToolHost(port.Object).GetTools(null, null).Single();

        var answer = await tool.InvokeAsync(new AIFunctionArguments { ["reason"] = "the registry is added" });

        tool.Name.Should().Be(Tool);
        answer!.ToString().Should().Contain("requested");
    }

    private static Task Run(
        SurfaceCapturingLoop loop, AgenticMasterContext context, MasterHandlerFixture.StubSubAgentRunner? children = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"), null,
                0, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

    private static AgenticMasterContext DesignContext()
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "the registry is added, go on"),
        ]);
        return context;
    }
}
