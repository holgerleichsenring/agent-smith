using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-13-8762m: hand_forward joins the coding master of a spec — never its children, a scan master
/// or a design turn — and only a spec-execution master start re-reads the memory index.
/// </summary>
public sealed class HandForwardSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string CodingMaster = "coding-agent-master";
    private const string Stale = "- [stale](stale.md) — the index as LoadMemoryIndex left it";

    [Fact]
    public async Task MasterToolComposition_CodingMasterWithSpec_CarriesHandForward()
    {
        var withSpec = new CapturingLoop();
        await Run(withSpec, CodingMaster, WithSpec(MasterHandlerFixture.BuildContext(CodingMaster)));
        var withoutSpec = new CapturingLoop();
        await Run(withoutSpec, CodingMaster, MasterHandlerFixture.BuildContext(CodingMaster));

        withSpec.MasterTools.Should().Contain(HandForwardToolHost.Name);
        withoutSpec.MasterTools.Should().NotContain(HandForwardToolHost.Name);
    }

    [Fact]
    public async Task MasterToolComposition_ScanDesignAndChildSurfaces_CarryNoHandForward()
    {
        var coding = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        await Run(coding, CodingMaster, WithSpec(MasterHandlerFixture.BuildContext(CodingMaster)), children, maxSubAgents: 20);
        var scan = new CapturingLoop();
        await Run(scan, "security-master", WithSpec(MasterHandlerFixture.BuildContext("security-master")), schema: "observation");
        var design = new CapturingLoop();
        await Run(design, DesignMaster, DesignContext(), limits: new LoopLimitsConfig());

        coding.MasterTools.Should().Contain(HandForwardToolHost.Name);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain(HandForwardToolHost.Name);
        scan.MasterTools.Should().NotContain(HandForwardToolHost.Name);
        design.MasterTools.Should().NotContain(HandForwardToolHost.Name);
    }

    [Fact]
    public async Task MasterToolComposition_EveryMasterWithARun_CarriesDisputeMemory_NoChild()
    {
        var coding = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        await Run(coding, CodingMaster, WithRun(WithSpec(MasterHandlerFixture.BuildContext(CodingMaster))), children, maxSubAgents: 20);
        var scan = new CapturingLoop();
        await Run(scan, "security-master", WithRun(MasterHandlerFixture.BuildContext("security-master")), schema: "observation");
        var design = new CapturingLoop();
        await Run(design, DesignMaster, WithRun(DesignContext()), limits: new LoopLimitsConfig());
        var noRun = new CapturingLoop();
        await Run(noRun, CodingMaster, MasterHandlerFixture.BuildContext(CodingMaster));

        // 2026-10-25-9324b: a dispute needs a run to publish on; a child acts for its master and disputes nothing.
        coding.MasterTools.Should().Contain(MemoryDisputeToolHost.Name);
        scan.MasterTools.Should().Contain(MemoryDisputeToolHost.Name);
        design.MasterTools.Should().Contain(MemoryDisputeToolHost.Name);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name).Should().NotContain(MemoryDisputeToolHost.Name);
        noRun.MasterTools.Should().NotContain(MemoryDisputeToolHost.Name);
    }

    private static AgenticMasterContext WithRun(AgenticMasterContext context)
    {
        context.Pipeline.Set(ContextKeys.RunId, "2026-10-25T1000-dispute");
        return context;
    }

    [Fact]
    public async Task AgenticMaster_DesignTurnAndScan_DoNotRereadTheMemoryIndex()
    {
        var scan = MasterHandlerFixture.BuildContext("security-master");
        scan.Pipeline.Set(ContextKeys.MemoryIndex, Stale);
        await Run(new CapturingLoop(), "security-master", scan, schema: "observation");
        var design = DesignContext();
        design.Pipeline.Set(ContextKeys.MemoryIndex, Stale);
        await Run(new CapturingLoop(), DesignMaster, design, limits: new LoopLimitsConfig());
        var spec = WithSpec(MasterHandlerFixture.BuildContext(CodingMaster));
        spec.Pipeline.Set(ContextKeys.MemoryIndex, Stale);
        await Run(new CapturingLoop(), CodingMaster, spec);

        scan.Pipeline.Get<string>(ContextKeys.MemoryIndex).Should().Be(Stale);
        design.Pipeline.Get<string>(ContextKeys.MemoryIndex).Should().Be(Stale);
        spec.Pipeline.Get<string>(ContextKeys.MemoryIndex).Should().NotBe(Stale, "a spec-execution start re-reads the index");
    }

    private static AgenticMasterContext WithSpec(AgenticMasterContext context)
    {
        context.Pipeline.Set(ContextKeys.CurrentSpec,
            new SpecDraft("2026-10-14-5678a", "build the checkout", "spec: 2026-10-14-5678a\ngoal: build the checkout\n", [])
            {
                Done = ["the checkout is built"],
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
            + "{\"pipeline_goal\":\"build the spec\",\"prior_context_slice\":\"the spec\"}}]";

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
