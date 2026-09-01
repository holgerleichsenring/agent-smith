using AgentSmith.Application.Services;
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
/// 2026-09-01-7df4: no shipped configuration states a context window for the scan role, so
/// neither the pressure finalizer nor the compactor was ever installed on that surface. At
/// 25 iterations that never mattered; at a raised ceiling the pass would walk into a
/// provider context refusal, which the master handler turns into a FAILED step — and a
/// failed scan is worse than a shallow one. The reduction ships with the ceiling.
/// </summary>
public sealed class ScanContextWindowTests
{
    private const int Window = 4000;      // tokens; the estimator counts 4 chars per token
    private const int MessageChars = 800;

    [Fact]
    public async Task ScanMaster_AtARaisedCeiling_ReducesInsteadOfOverflowing()
    {
        var reducing = new RecordingChat();
        await ScanClient(reducing, compaction: true, window: Window)
            .GetResponseAsync(Sweep(20), ToolOptions(), CancellationToken.None);

        reducing.Forwarded[^1].Count.Should().BeLessThan(22,
            "the evicted middle is replaced by a summary");
        reducing.Forwarded[^1].Should().Contain(
            m => m.Text != null && m.Text.Contains("[Context summary"));

        var overflowing = new RecordingChat();
        await ScanClient(overflowing, compaction: true, window: null)
            .GetResponseAsync(Sweep(20), ToolOptions(), CancellationToken.None);

        overflowing.Forwarded[0].Should().HaveCount(22,
            "with no window stated anywhere the chain is the one that overflowed");
    }

    [Fact]
    public async Task ScanMaster_PastTheHardBound_FinalisesRatherThanRefusing()
    {
        var chat = new RecordingChat();

        await ScanClient(chat, compaction: false, window: Window)
            .GetResponseAsync(Sweep(24), ToolOptions(), CancellationToken.None);

        chat.Options[^1]!.ToolMode.Should().Be(ChatToolMode.None, "the exploration ends here");
        chat.Forwarded[^1][^1].Text.Should().Contain("reply now with your final answer");
    }

    [Fact]
    public void ScanMaster_ARoleThatStatesItsOwnWindow_KeepsIt()
    {
        var agent = Agent(compaction: true);
        agent.Models!.Primary!.ContextWindowTokens = 128000;

        NewFactory(new RecordingChat())
            .GetContextWindowTokens(agent, TaskType.Primary).Should().Be(128000,
                "the window is a property of the deployment, not of the calling surface");
    }

    private static IChatClient ScanClient(RecordingChat chat, bool compaction, int? window)
    {
        var agent = Agent(compaction);
        return NewFactory(chat).Create(
            agent, TaskType.Primary, maxIterations: 100, masterLoopHooks: null,
            agent.Compaction, contextWindowOverride: window);
    }

    private static AgentConfig Agent(bool compaction) => new()
    {
        Type = "stub",
        Model = "m",
        Models = new ModelRegistryConfig { Primary = new ModelAssignment { Model = "m" } },
        Compaction = new CompactionConfig
        {
            IsEnabled = compaction,
            MaxContextTokens = 200000,
            MaxContextTokensTriggerRatio = 0.7,
            KeepRecentIterations = 3,
        },
    };

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

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Forwarded.Add(messages.ToList());
            Options.Add(options);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
