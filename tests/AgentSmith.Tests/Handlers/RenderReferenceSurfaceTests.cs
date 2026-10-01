using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.Browser;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-01-283de: render_reference is on the design MASTER of a turn that seeded its project —
/// never on its children, never on a coding master (that surface is 2026-10-01-283df's).
/// </summary>
public sealed class RenderReferenceSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private readonly RenderReferenceToolFactory _factory = new(new BrowserRenderFixture().Services());

    [Fact]
    public async Task DesignTurn_SeededProject_CarriesRenderReference_ItsChildrenDoNot()
    {
        var loop = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        var context = DesignContext();
        context.Pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"),
                subAgents: children, limits: new LoopLimitsConfig(), render: _factory)
            .ExecuteAsync(context, CancellationToken.None);

        loop.MasterTools.Should().Contain("render_reference");
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain("render_reference");
    }

    [Fact]
    public async Task CodingMaster_WithProject_HasNoRenderReference()
    {
        var loop = new CapturingLoop();
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });
        context.Pipeline.Set(ContextKeys.DialogueJobId, "conv");

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"),
                render: _factory)
            .ExecuteAsync(context, CancellationToken.None);

        loop.MasterTools.Should().NotContain("render_reference");
    }

    private static AgenticMasterContext DesignContext()
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set(ContextKeys.DialogueJobId, "conv");
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "make it look like the upload")]);
        return context;
    }

    private sealed class CapturingLoop(bool spawn = false) : IAgenticLoopRunner
    {
        private const string OneTask = "[{\"name\":\"RepoScout\",\"activity\":\"read the sources\","
            + "\"task_description\":\"answer one question\",\"inherited_context\":"
            + "{\"pipeline_goal\":\"cut the phase\",\"prior_context_slice\":\"the transcript\"}}]";

        public IReadOnlyList<string> MasterTools { get; private set; } = [];

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (MasterTools.Count == 0)
            {
                MasterTools = [.. request.Tools!.OfType<AIFunction>().Select(t => t.Name)];
                if (spawn)
                    await request.Tools!.OfType<AIFunction>().Single(t => t.Name == "spawn_agents").InvokeAsync(
                        new AIFunctionArguments { ["tasks"] = JsonDocument.Parse(OneTask).RootElement }, cancellationToken);
            }
            return new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1));
        }
    }
}
