using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-10-13-8762o: decide_by_default joins the coding master and the design partner — never a scan master
/// or a child — and (2026-10-25-9324e) a coding child's remember of a feedback entry publishes no pointer row.
/// </summary>
public sealed class DefaultDecisionSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string CodingMaster = "coding-agent-master";

    [Fact]
    public async Task MasterToolComposition_CodingAndDesignMasters_CarryDecideByDefault()
    {
        var coding = new CapturingLoop();
        await Run(coding, CodingMaster, WithRun(MasterHandlerFixture.BuildContext(CodingMaster)));
        var design = new CapturingLoop();
        await Run(design, DesignMaster, DesignContext(), limits: new LoopLimitsConfig());
        var noRun = new CapturingLoop();
        await Run(noRun, CodingMaster, MasterHandlerFixture.BuildContext(CodingMaster));

        coding.MasterTools.Select(t => t.Name).Should().Contain(DefaultDecisionToolHost.Name);
        design.MasterTools.Select(t => t.Name).Should().Contain(DefaultDecisionToolHost.Name);
        noRun.MasterTools.Select(t => t.Name).Should().NotContain(DefaultDecisionToolHost.Name, "no run id, no row to write");
    }

    [Fact]
    public async Task MasterToolComposition_ScanMasterAndChildren_CarryNoDecideByDefault()
    {
        var coding = new CapturingLoop(spawn: true);
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        await Run(coding, CodingMaster, WithRun(MasterHandlerFixture.BuildContext(CodingMaster)), children, maxSubAgents: 20);
        var scan = new CapturingLoop();
        await Run(scan, "security-master", WithRun(MasterHandlerFixture.BuildContext("security-master")), schema: "observation");

        coding.MasterTools.Select(t => t.Name).Should().Contain(DefaultDecisionToolHost.Name);
        children.SeenContexts.Single().ChildTools.Select(t => t.Name)
            .Should().Contain("remember").And.NotContain(DefaultDecisionToolHost.Name);
        scan.MasterTools.Select(t => t.Name).Should().NotContain(DefaultDecisionToolHost.Name);
    }

    [Fact]
    public async Task Remember_FromACodingChild_PublishesNothingAndWaitsForNoOne()
    {
        var events = new RecordingEventPublisher();
        var children = new MasterHandlerFixture.StubSubAgentRunner();
        var context = WithRun(MasterHandlerFixture.BuildContext(CodingMaster));
        context.Pipeline.Set(ContextKeys.CheckpointedRepos, new Dictionary<string, bool> { ["default"] = true });
        await Run(new CapturingLoop(spawn: true), CodingMaster, context, children, maxSubAgents: 20, events: events);

        var remember = children.SeenContexts.Single().ChildTools.OfType<AIFunction>().Single(t => t.Name == "remember");
        var answer = await remember.InvokeAsync(new AIFunctionArguments
        {
            ["type"] = "feedback", ["name"] = "child-rule", ["description"] = "a rule", ["body"] = "the child's body",
        });

        // 2026-10-25-9324e: no memory waits for a person's ratification.
        answer?.ToString().Should().Contain("recorded in default").And.NotContain("PROPOSED").And.NotContain("ratif");
        events.Events.OfType<DefaultDecisionTakenEvent>().Should().BeEmpty();
    }

    private static AgenticMasterContext WithRun(AgenticMasterContext context)
    {
        context.Pipeline.Set(ContextKeys.RunId, "2026-10-14T1100-surface");
        return context;
    }

    private static AgenticMasterContext DesignContext()
    {
        var context = WithRun(MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false));
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
        [
            new SpecDialogTurn(SpecDialogTurn.UserRole, "we need widgets"),
            new SpecDialogTurn(SpecDialogTurn.AssistantRole, "Two open questions.", SpecDialogTurnKind.Answer),
            new SpecDialogTurn(SpecDialogTurn.UserRole, "build the checkout frame"),
        ]);
        return context;
    }

    private static Task Run(
        CapturingLoop loop, string master, AgenticMasterContext context,
        MasterHandlerFixture.StubSubAgentRunner? children = null, int maxSubAgents = 0,
        LoopLimitsConfig? limits = null, string? schema = null, IEventPublisher? events = null) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(master, "body"), schema,
                maxSubAgents, subAgents: children ?? new MasterHandlerFixture.StubSubAgentRunner(),
                limits: limits, recordEvents: events)
            .ExecuteAsync(context, CancellationToken.None);

    /// <summary>Records the master's tools and, when asked, spawns one child so its surface is seen.</summary>
    private sealed class CapturingLoop(bool spawn = false) : IAgenticLoopRunner
    {
        private const string OneTask = "[{\"name\":\"RepoScout\",\"activity\":\"read the sources\","
            + "\"task_description\":\"answer one question\",\"inherited_context\":"
            + "{\"pipeline_goal\":\"build the spec\",\"prior_context_slice\":\"the spec\"}}]";

        public IReadOnlyList<AIFunction> MasterTools { get; private set; } = [];

        public async Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            if (MasterTools.Count == 0)
            {
                MasterTools = [.. request.Tools!.OfType<AIFunction>()];
                if (spawn)
                    await MasterTools.Single(t => t.Name == "spawn_agents").InvokeAsync(
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
