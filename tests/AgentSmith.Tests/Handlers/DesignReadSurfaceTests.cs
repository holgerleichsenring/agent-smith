using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-01-7f7ab: which masters carry design_read — design and coding masters of a project with
/// a figma source, never a scan master, never a project without one, and never a child.
/// </summary>
public sealed class DesignReadSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string CodingMaster = "coding-agent-master";
    private static readonly DesignSource Figma = new("brand", DesignSourceVendor.Figma, "figma-token");

    [Fact]
    public async Task MasterToolComposition_ProjectWithoutDesignSource_HasNoDesignRead()
    {
        var loop = new CapturingLoop();
        var context = MasterHandlerFixture.BuildContext(CodingMaster);
        context.Pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p" });

        await Run(loop, CodingMaster, context);

        loop.MasterTools.Should().NotContain("design_read");
    }

    [Fact]
    public async Task CodingMaster_ProjectWithFigmaSource_CarriesDesignRead_ItsChildrenDoNot()
    {
        var loop = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        var context = MasterHandlerFixture.BuildContext(CodingMaster);
        context.Pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", DesignSources = [Figma] });

        await Run(loop, CodingMaster, context, children, maxSubAgents: 20);

        loop.MasterTools.Should().Contain("design_read");
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should()
            .NotContain("design_read", "children fanning out over one file would share one rate limit");
    }

    [Fact]
    public async Task DesignTurn_SeededFigmaSource_CarriesDesignRead_ItsChildrenDoNot()
    {
        var loop = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        var context = DesignContext();
        context.Pipeline.Set<IReadOnlyList<DesignSource>>(ContextKeys.SpecDialogDesignSources, [Figma]);

        await Run(loop, DesignMaster, context, children, limits: new LoopLimitsConfig());

        loop.MasterTools.Should().Contain("design_read");
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain("design_read");
    }

    [Fact]
    public async Task ScanMaster_ProjectWithFigmaSource_HasNoDesignRead()
    {
        var loop = new CapturingLoop();
        var context = MasterHandlerFixture.BuildContext("security-master");
        context.Pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject { Name = "p", DesignSources = [Figma] });

        await Run(loop, "security-master", context, schema: "observation");

        loop.MasterTools.Should().NotContain("design_read");
    }

    private static Task Run(
        CapturingLoop loop, string master, AgenticMasterContext context,
        MasterHandlerFixture.StubSubAgentRunner? children = null, int maxSubAgents = 0,
        LoopLimitsConfig? limits = null, string? schema = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(master, "body"), schema,
                maxSubAgents, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(),
                limits: limits, figma: Mock.Of<IFigmaClient>(MockBehavior.Strict))
            .ExecuteAsync(context, CancellationToken.None);

    private static AgenticMasterContext DesignContext()
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "we need widgets"),
            new SpecDialogTurn(SpecDialogTurn.AssistantRole, "Two open questions.", SpecDialogTurnKind.Answer),
            new SpecDialogTurn(SpecDialogTurn.UserRole, "build the checkout frame"),
        ]);
        return context;
    }

    /// <summary>Records the master's tools and, when asked, spawns one child so its surface is seen.</summary>
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
