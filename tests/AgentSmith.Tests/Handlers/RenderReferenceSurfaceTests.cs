using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Browser;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-01-283de: render_reference is on the design MASTER of a turn that seeded its project —
/// never on its children. 2026-10-01-283df: and on a run's coding master exactly when the project's
/// config enables the browser, never because a dialog seed happens to be present.
/// </summary>
public sealed class RenderReferenceSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private readonly RenderReferenceToolFactory _factory = new BrowserRenderFixture().Factory();

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

    // 2026-10-02-075dc: a turn with an upload gets run_in_reference on the master only.
    [Fact]
    public async Task DesignTurn_WithAnUpload_HasRunInReference_ItsChildrenDoNot()
    {
        var loop = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        var context = DesignContext();
        context.Pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });
        var map = new Dictionary<string, ISandbox>(context.Pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes))
        {
            ["reference:app"] = new AgentSmith.Tests.References.InMemoryFileSandbox(),
        };
        context.Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, map);

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"),
                subAgents: children, limits: new LoopLimitsConfig(), render: _factory)
            .ExecuteAsync(context, CancellationToken.None);

        loop.MasterTools.Should().Contain("run_in_reference");
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain("run_in_reference");
    }

    [Fact]
    public async Task DesignTurn_WithoutAnUpload_HasNoRunInReference()
    {
        var loop = new CapturingLoop();
        var context = DesignContext();
        context.Pipeline.Set(ContextKeys.SpecDialogProject, new ResolvedProject { Name = "p" });

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(DesignMaster, "body"),
                render: _factory)
            .ExecuteAsync(context, CancellationToken.None);

        loop.MasterTools.Should().NotContain("run_in_reference");
    }

    [Fact]
    public async Task CodingMaster_DialogSeedsButNoBrowserConfig_HasNoRenderReference()
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CodingMaster_ProjectBrowserConfig_DecidesRenderReference(bool enabled)
    {
        var loop = new CapturingLoop();
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "p", Sandbox = new SandboxConfig { Browser = new ProjectBrowserConfig { Enabled = enabled } },
        });

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"),
                render: _factory)
            .ExecuteAsync(context, CancellationToken.None);

        if (enabled) loop.MasterTools.Should().Contain("render_reference");
        else loop.MasterTools.Should().NotContain("render_reference");
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
