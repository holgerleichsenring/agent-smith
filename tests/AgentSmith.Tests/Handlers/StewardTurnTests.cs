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
/// 2026-10-14-da5ef: a turn the ticket steward leads gets its own contract and surface, and its reply is an answer —
/// no proposal rule, refusal, draft parse or review runs, so a quoted ```yaml block is never read as a draft. The
/// sub-agents of every dialog turn read and do nothing else.
/// </summary>
public sealed class StewardTurnTests
{
    private const string DesignMaster = "design-partner-master";
    private const string QuotedYaml = "Attempt 2 ran this spec:\n\n```yaml\nspec: p9001\ngoal: \"cut the widget\"\n```\n\nIt stopped on a missing registry.";

    [Fact]
    public async Task StewardTurn_PromptHasNoProposeContract_AndPublishesAnAnswerOutcome()
    {
        var loop = new ReplyingLoop(QuotedYaml);
        var context = TurnContext(PipelinePresets.TicketStewardMaster);

        await Run(loop, context);

        loop.Requests.Should().ContainSingle("no refusal, outcome fix or review re-prompts a steward turn");
        var prompt = loop.Requests[0].UserPrompt;
        prompt.Should().Contain("## Ticket conversation").And.Contain("[user] why did attempt 2 stop?")
            .And.Contain("Propose nothing");
        prompt.Should().NotContain("MAY propose").And.NotContain("MAY NOT propose").And.NotContain("## Design conversation");
        context.Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome).Should().BeOfType<AnswerOutcome>();
    }

    [Fact]
    public async Task StewardTurn_QuotedYamlBlock_IsNotADraft()
    {
        var loop = new ReplyingLoop(QuotedYaml);
        var context = TurnContext(PipelinePresets.TicketStewardMaster);

        await Run(loop, context);

        context.Pipeline.Get<OutcomeProposal>(ContextKeys.SpecDialogOutcome).Should().BeOfType<AnswerOutcome>(
            "a block the steward quotes is text, not a spec proposal");
        context.Pipeline.Get<string>(ContextKeys.MasterAnswer).Should().Be(QuotedYaml);
    }

    [Fact]
    public async Task DesignTurn_SameQuotedYaml_StillPassesTheProposalRule()
    {
        var loop = new ReplyingLoop(QuotedYaml);

        await Run(loop, TurnContext(DesignMaster));

        loop.Requests.Should().HaveCountGreaterThan(1, "the design partner may not propose on a first turn and is re-prompted");
        loop.Requests[0].UserPrompt.Should().Contain("## Design conversation").And.Contain("MAY NOT propose");
    }

    [Fact]
    public async Task StewardTurn_Tools_HaveSeriesAndStart_NotWithdraw()
    {
        var loop = new ReplyingLoop("Attempt 2 stopped on a missing registry.");

        await Run(loop, TurnContext(PipelinePresets.TicketStewardMaster));

        loop.Tools.Should().Contain(["start_next_attempt", "read_series", "edit_series", "verify_series",
            "spawn_agents", "read_sub_agent_observations", "read_ticket", "ticket_runs", "read_file"]);
        loop.Tools.Should().NotContain(TicketStewardTools.Excluded);
    }

    [Fact]
    public async Task DesignTurn_SameSeeds_KeepsWithdrawAndDecideByDefault()
    {
        var loop = new ReplyingLoop("An answer.");

        await Run(loop, TurnContext(DesignMaster));

        loop.Tools.Should().Contain("withdraw_filed_ticket").And.Contain(DefaultDecisionToolHost.Name)
            .And.Contain("start_next_attempt");
    }

    [Theory]
    [InlineData(DesignMaster)]
    [InlineData(PipelinePresets.TicketStewardMaster)]
    public async Task SpecDialogSubAgent_ChildTools_AreReadToolsOnly(string master)
    {
        var children = new MasterHandlerFixture.StubSubAgentRunner();

        await Run(new SurfaceCapturingLoop(spawn: true), TurnContext(master), children);

        var childTools = children.SeenContexts.Single().ChildTools.Select(t => t.Name).ToList();
        childTools.Should().Contain(["read_file", "read_ticket", "ticket_runs", "recall"]);
        childTools.Should().OnlyContain(name => SpecDialogChildTools.ReadTools.Contains(name));
        childTools.Should().NotContain(["withdraw_filed_ticket", "start_next_attempt", "edit_series", "verify_series",
            "ask_human", "remember", "http_request"]);
    }

    private static Task Run(
        IAgenticLoopRunner loop, AgenticMasterContext context, MasterHandlerFixture.StubSubAgentRunner? children = null) =>
        BoundDialogTurn.Run(loop, context, children);

    private static AgenticMasterContext TurnContext(string master) => BoundDialogTurn.Context(master);

    /// <summary>Records every request it is handed and answers each with the same reply.</summary>
    private sealed class ReplyingLoop(string reply) : IAgenticLoopRunner
    {
        public List<AgenticLoopRequest> Requests { get; } = [];

        public IReadOnlyList<string> Tools => [.. Requests[0].Tools!.OfType<AIFunction>().Select(t => t.Name)];

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, reply))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
