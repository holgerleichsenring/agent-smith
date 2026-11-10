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
/// 2026-11-10-fc66: a design turn is offered the tools a conversation uses. Its questions go into the reply with the
/// analysis, so it carries no mid-turn ask, no default filed for later, no any-method HTTP, no memory write it would
/// be refused, and one way each to search and to list. The coding master keeps everything it had.
/// </summary>
public sealed class DesignTurnToolSurfaceTests
{
    private const string DesignMaster = "design-partner-master";
    private const string CodingMaster = "coding-agent-master";

    private static readonly string[] Removed =
        ["ask_human", "decide_by_default", "http_request", "remember", "list_directory", "grep_in_file"];

    [Fact]
    public async Task SpecDialogSurface_OffersOnlyConversationTools()
    {
        var loop = new CapturingLoop();

        await Run(loop, DesignMaster, DesignContext());

        var names = loop.MasterTools.Select(t => t.Name).ToList();
        names.Should().NotContain(Removed);
        names.Should().Contain(["read_file", "grep_in_tree", "find_files", "directory_tree",
            "spawn_agents"]);
    }

    /// <summary>2026-11-10-14a3d: the memory and withdrawal tools come with what they work on. The turn carries its
    /// dialogue identity, as every design turn does; the withdrawal port is seeded only once something was filed.</summary>
    [Fact]
    public async Task DesignSurface_SourceUploadNoDesignNoMemoryNoFiling_HasNoVisualMockMemoryOrWithdrawTools()
    {
        var bare = new CapturingLoop();
        var withStoreAndFiling = new CapturingLoop();
        var context = DesignContext();
        context.Pipeline.Set(ContextKeys.MemoryIndex, "- [A fact](a-fact.md) — one line");
        context.Pipeline.Set(ContextKeys.SpecDialogWithdrawal, Moq.Mock.Of<Contracts.Dialogue.IFiledTicketWithdrawal>());

        await Run(bare, DesignMaster, DesignContext());
        await Run(withStoreAndFiling, DesignMaster, context);

        bare.MasterTools.Select(t => t.Name).Should().NotContain(["recall", "dispute_memory", "withdraw_filed_ticket"]);
        withStoreAndFiling.MasterTools.Select(t => t.Name).Should().Contain(["recall", "dispute_memory", "withdraw_filed_ticket"]);
    }

    [Fact]
    public async Task CodingSurface_KeepsAskHumanDecideByDefaultAndTheFullReadSet()
    {
        var loop = new CapturingLoop();
        var context = MasterHandlerFixture.BuildContext(CodingMaster);
        context.Pipeline.Set(ContextKeys.RunId, "2026-11-10T1200-coding");

        await Run(loop, CodingMaster, context);

        loop.MasterTools.Select(t => t.Name).Should().Contain(Removed);
    }

    [Fact]
    public void SpecDialogChildTools_KeepsOnlyTheReadSet()
    {
        var surface = new List<AITool>(
            Removed.Concat(["read_file", "grep_in_tree", "find_files", "directory_tree", "withdraw_filed_ticket"])
                .Select(name => AIFunctionFactory.Create(() => "ok", name)));

        SpecDialogChildTools.Of(surface).Select(t => t.Name)
            .Should().BeEquivalentTo(["read_file", "grep_in_tree", "find_files", "directory_tree"]);
    }

    private static AgenticMasterContext DesignContext()
    {
        var context = MasterHandlerFixture.BuildContext(DesignMaster, includeTicket: false);
        context.Pipeline.Set(ContextKeys.RunId, "2026-11-10T1200-design");
        context.Pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        context.Pipeline.Set(ContextKeys.DialogueJobId, "s-design"); // every design turn carries its dialogue identity
        context.Pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "build the checkout frame")]);
        return context;
    }

    private static Task Run(CapturingLoop loop, string master, AgenticMasterContext context) =>
        MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog(master, "body"), null,
                maxSubAgents: 4, subAgents: new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

    /// <summary>2026-11-10-85e2: a design turn's loop request carries hooks whose OnFold feeds the turn's fold count, so
    /// a real fold in the compactor makes outcome_format return a format again; the loop stays ungoverned.</summary>
    [Fact]
    public async Task DesignTurn_RealFoldInTheCompactor_MakesOutcomeFormatReturnTheFormatAgain()
    {
        var loop = new CapturingLoop();
        var context = DesignContext();
        await Run(loop, DesignMaster, context);
        var hooks = loop.Hooks!;
        var references = new Moq.Mock<Contracts.Services.ISkillReferenceSource>();
        references.Setup(r => r.TryRead("design-outcome-spec")).Returns("## Drafting a spec\n");
        var format = new OutcomeFormatToolHost(references.Object, TurnFolds.Of(context.Pipeline)).GetTools(null, null).Single();
        var compactor = new AgentSmith.Infrastructure.Services.Providers.Agent.CompactingChatClient(
            new AnswerClient(), new CompactionConfig
            {
                IsEnabled = true, MaxContextTokens = 800, MaxContextTokensTriggerRatio = 1.0, KeepRecentIterations = 3,
            }, hooks, (_, _) => Task.FromResult("SUMMARY"));

        var first = (await format.InvokeAsync(new AIFunctionArguments { ["kind"] = "spec" }))!.ToString();
        await compactor.GetResponseAsync(Conversation(rounds: 12));
        var again = (await format.InvokeAsync(new AIFunctionArguments { ["kind"] = "spec" }))!.ToString();

        hooks.Governs.Should().BeFalse("a design turn's loop gets no governor, as before");
        first.Should().Be("## Drafting a spec");
        again.Should().Be("## Drafting a spec", "the compactor folded the turn, so the first copy may be gone");
    }

    private static List<ChatMessage> Conversation(int rounds)
    {
        var list = new List<ChatMessage> { new(ChatRole.System, "SYSTEM"), new(ChatRole.User, "prompt") };
        for (var i = 0; i < rounds; i++)
        {
            list.Add(new ChatMessage(ChatRole.Assistant, $"turn-{i}|" + new string('x', 200)));
            list.Add(new ChatMessage(ChatRole.User, $"result-{i}|" + new string('y', 200)));
        }
        return list;
    }

    private sealed class AnswerClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class CapturingLoop : IAgenticLoopRunner
    {
        public IReadOnlyList<AIFunction> MasterTools { get; private set; } = [];

        public Contracts.Providers.MasterLoopHooks? Hooks { get; private set; }

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken cancellationToken)
        {
            Hooks ??= request.MasterLoopHooks;
            if (MasterTools.Count == 0) MasterTools = [.. request.Tools!.OfType<AIFunction>()];
            return Task.FromResult(new AgenticLoopResult(
                new ChatResponse(new ChatMessage(ChatRole.Assistant, "An answer with no outcome block."))
                {
                    Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
