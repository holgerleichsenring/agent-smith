using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

/// <summary>
/// 2026-08-27-3eb1: the repository sweep is ONE GetResponseAsync whose tool results all
/// land in one message list. These tests drive the REAL factory chain for TaskType.Scout
/// and assert what reaches the provider — the reduction that only the coding master used
/// to get, and the finalise-under-pressure exit when reduction is not enough or is off.
/// </summary>
public sealed class ScoutContextWindowTests
{
    private const int Window = 4000;      // tokens; the estimator counts 4 chars per token
    private const int MessageChars = 800;

    [Fact]
    public async Task Scout_ALongSweep_FoldsBeforeTheProviderRefuses()
    {
        var chat = new RecordingChat();
        var client = ScoutClient(chat, Agent(Window, compaction: true));

        await client.GetResponseAsync(Sweep(20), ToolOptions(), CancellationToken.None);

        var forwarded = chat.Forwarded[^1];
        forwarded.Count.Should().BeLessThan(22, "the evicted middle is replaced by a summary");
        forwarded.Should().Contain(m => m.Text != null && m.Text.Contains("[Context summary"));
    }

    [Fact]
    public async Task Scout_AShortSweep_IsUnchangedAndFoldsNothing()
    {
        var chat = new RecordingChat();
        var client = ScoutClient(chat, Agent(Window, compaction: true));

        await client.GetResponseAsync(Sweep(2), ToolOptions(), CancellationToken.None);

        chat.Forwarded.Should().ContainSingle("nothing to fold means no summarizer call");
        chat.Forwarded[0].Should().HaveCount(4);
        chat.Options[0]!.ToolMode.Should().BeNull("a short sweep keeps exploring");
    }

    [Fact]
    public async Task Scout_CompactionUnconfigured_BehavesAsItDoesToday()
    {
        var chat = new RecordingChat();
        var client = ScoutClient(chat, Agent(Window, compaction: false));

        // Above the fold trigger, below the finalize bound: only compaction is at stake.
        await client.GetResponseAsync(Sweep(14), ToolOptions(), CancellationToken.None);

        chat.Forwarded[0].Should().HaveCount(16, "no fold, no summary, nothing rewritten");
        chat.Options[0]!.ToolMode.Should().BeNull();
    }

    [Fact]
    public async Task Role_TheWindowIsAbsent_NoThresholdIsDerived()
    {
        var chat = new RecordingChat();
        var agent = Agent(window: null, compaction: true);
        var client = ScoutClient(chat, agent);

        await client.GetResponseAsync(Sweep(40), ToolOptions(), CancellationToken.None);

        NewFactory(chat).GetContextWindowTokens(agent, TaskType.Scout).Should().BeNull();
        chat.Forwarded[0].Should().HaveCount(42, "an unstated window derives nothing at all");
        chat.Options[0]!.ToolMode.Should().BeNull();
    }

    [Fact]
    public void Role_TheWindowIsStated_ItReachesTheChain()
    {
        var agent = Agent(Window, compaction: true);

        NewFactory(new RecordingChat())
            .GetContextWindowTokens(agent, TaskType.Scout).Should().Be(Window);
    }

    [Fact]
    public async Task Sweep_ApproachingTheWindow_FinalisesInsteadOfOverflowing()
    {
        var chat = new RecordingChat();
        var client = ScoutClient(chat, Agent(Window, compaction: false));

        await client.GetResponseAsync(Sweep(24), ToolOptions(), CancellationToken.None);

        var options = chat.Options[^1]!;
        options.ToolMode.Should().Be(ChatToolMode.None, "the exploration ends here");
        options.Tools.Should().NotBeNullOrEmpty("a tool-bearing history needs the tools declared");
        chat.Forwarded[^1][^1].Text.Should().Contain("reply now with your final answer");
    }

    // The caller's ChatOptions is shared by FunctionInvokingChatClient across every
    // iteration — finalising must not leave ToolMode=None behind on it.
    [Fact]
    public async Task Sweep_Finalised_DoesNotMutateTheCallersOptions()
    {
        var chat = new RecordingChat();
        var client = ScoutClient(chat, Agent(Window, compaction: false));
        var options = ToolOptions();

        await client.GetResponseAsync(Sweep(24), options, CancellationToken.None);

        options.ToolMode.Should().BeNull();
    }

    [Fact]
    public async Task Sweep_Finalised_StillReturnsAParseableMap()
    {
        var chat = new RecordingChat { Reply = """{"primary_language": "csharp"}""" };
        var client = ScoutClient(chat, Agent(Window, compaction: false));

        var response = await client.GetResponseAsync(Sweep(24), ToolOptions(), CancellationToken.None);

        chat.Options[^1]!.ToolMode.Should().Be(ChatToolMode.None, "the turn was finalised");
        new ProjectMapJsonReader()
            .TryRead(response.Text ?? string.Empty, out var map, out _).Should().BeTrue();
        map!.PrimaryLanguage.Should().Be("csharp");
    }

    private static IChatClient ScoutClient(RecordingChat chat, AgentConfig agent) =>
        NewFactory(chat).Create(agent, TaskType.Scout, 25, masterLoopHooks: null, agent.Compaction);

    private static AgentConfig Agent(int? window, bool compaction)
    {
        var scout = new ModelAssignment { Model = "m", ContextWindowTokens = window };
        return new AgentConfig
        {
            Type = "stub",
            Model = "m",
            Models = new ModelRegistryConfig { Scout = scout },
            Compaction = new CompactionConfig
            {
                IsEnabled = compaction,
                MaxContextTokens = 200000,
                MaxContextTokensTriggerRatio = 0.7,
                KeepRecentIterations = 3,
            },
        };
    }

    private static ChatOptions ToolOptions() => new()
    {
        Tools = [AIFunctionFactory.Create(() => "ok", "probe")],
    };

    private static List<ChatMessage> Sweep(int toolRounds)
    {
        var list = new List<ChatMessage>
        {
            new(ChatRole.System, "SYSTEM"),
            new(ChatRole.User, "USER"),
        };
        for (var i = 0; i < toolRounds; i++)
            list.Add(new ChatMessage(ChatRole.Assistant, new string((char)('a' + i % 26), MessageChars)));
        return list;
    }

    private static ChatClientFactory NewFactory(RecordingChat chat) =>
        new(
            [new StubBuilder(chat)],
            EventTestStubs.NoOp,
            EventTestStubs.RunContext,
            new ModelPricingResolver(),
            new AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry(
                NullLogger<AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry>.Instance),
            new AgentSmith.Infrastructure.Services.RateLimiting.ThrottleWaitReporter(),
            new AgentSmith.Contracts.Runs.NullRunTraceWriter(),
            new CompactionSummaryRequest(),
            new WindowDerivedCompaction(),
            NullLoggerFactory.Instance);

    private sealed class StubBuilder(RecordingChat chat) : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["stub"];

        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => chat;
    }

    private sealed class RecordingChat : IChatClient
    {
        public List<List<ChatMessage>> Forwarded { get; } = [];
        public List<ChatOptions?> Options { get; } = [];
        public string Reply { get; init; } = "ok";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Forwarded.Add(messages.ToList());
            Options.Add(options);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, Reply)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
