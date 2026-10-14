using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// run_gate_stage joins the coding master of a run that declares a stage — never its children,
/// a scan master or a design turn.
/// </summary>
public sealed class GateStageSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string CodingMaster = "coding-agent-master";

    [Fact]
    public async Task MasterToolComposition_CodingMaster_CarriesRunGateStage_ChildScanAndDesignTurnDoNot()
    {
        var coding = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        await Run(coding, CodingMaster, Declare(MasterHandlerFixture.BuildContext(CodingMaster)), children, maxSubAgents: 20);
        var scan = new CapturingLoop();
        await Run(scan, "security-master", Declare(MasterHandlerFixture.BuildContext("security-master")), schema: "observation");
        var design = new CapturingLoop();
        await Run(design, DesignMaster, Declare(DesignContext()), limits: new LoopLimitsConfig());

        coding.MasterTools.Should().Contain(GateStageToolHost.Name);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain(GateStageToolHost.Name);
        scan.MasterTools.Should().NotContain(GateStageToolHost.Name);
        design.MasterTools.Should().NotContain(GateStageToolHost.Name);
    }

    private static AgenticMasterContext Declare(AgenticMasterContext context)
    {
        context.Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            new Dictionary<string, string> { ["default"] = "shop" });
        context.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
            {
                ["default"] = [new("default", ".", "csharp", Verify: [new ContextYamlVerifyStage("build", "dotnet build")])],
            });
        return context;
    }

    private static Task Run(
        CapturingLoop loop, string master, AgenticMasterContext context,
        MasterHandlerFixture.StubSubAgentRunner? children = null, int maxSubAgents = 0,
        LoopLimitsConfig? limits = null, string? schema = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(master, "body"), schema,
                maxSubAgents, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(),
                limits: limits)
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
            + "{\"pipeline_goal\":\"cut the spec\",\"prior_context_slice\":\"the transcript\"}}]";

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
