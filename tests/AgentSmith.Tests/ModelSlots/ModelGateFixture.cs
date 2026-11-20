using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.Factories;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>
/// 2026-11-20-24bd: the real chat client factory with the model gate over an in-process place store and a provider
/// that records which runs are inside it at once — the stack every master, sub-agent and design turn call passes.
/// </summary>
internal sealed class ModelGateFixture
{
    // not a limit: a poll no test ever reaches, so only a release can wake a waiter unless a test asks for polls
    private static readonly TimeSpan NoPoll = TimeSpan.FromDays(1);

    /// <param name="pollInterval">How often a waiter asks again; never unless given.</param>
    /// <param name="storeSignal">The store's own signal, as a store on another replica would have: its releases
    /// neither wake this gate's waiters nor clear its holds.</param>
    public ModelGateFixture(TimeSpan? pollInterval = null, ModelRunSlotSignal? storeSignal = null)
    {
        Slots = new InProcessModelRunSlots(storeSignal ?? Signal, Clock);
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddTransient(_ => new ConfiguredAgents([.. Configured.Values]));
        Gate = new ModelRunGate(new CountingSlots(Slots, this), Signal, RunContext,
            new ModelRunWaitLine(RunContext, TurnActivity, Events, NullLogger<ModelRunWaitLine>.Instance),
            NullLogger<ModelRunGate>.Instance, services.BuildServiceProvider(), Clock) { PollInterval = pollInterval ?? NoPoll };
        Factory = new ChatClientFactory(
            [new Builder(Provider)], Events, RunContext, new ModelPricingResolver(),
            new LlmRateLimiterRegistry(NullLogger<LlmRateLimiterRegistry>.Instance),
            new ThrottleWaitReporter(), new NullRunTraceWriter(), TurnActivity, new CompactionSummaryRequest(),
            new WindowDerivedCompaction(), new AgentSmith.Infrastructure.Services.ToolImages.ToolImageRelay(new(), new()),
            SecretValueMaskingChatClientTests.Masker(), NullLoggerFactory.Instance, TestCredentials.EnvironmentOnly,
            runGate: Gate);
    }

    /// <summary>The agents the configuration holds now; a test edits it as the studio would.</summary>
    public Dictionary<string, AgentConfig> Configured { get; } = new();

    public ModelSlotsClock Clock { get; } = new();
    public ModelRunSlotSignal Signal { get; } = new();
    public InProcessModelRunSlots Slots { get; }
    public AsyncLocalRunContextAccessor RunContext { get; } = new();
    public AsyncLocalTurnActivityObserverAccessor TurnActivity { get; } = new();
    public RecordingEventPublisher Events { get; } = new();
    public ProbeProvider Provider { get; } = new();
    public ModelRunGate Gate { get; }
    public ChatClientFactory Factory { get; }

    /// <summary>How often the gate asked the store for a place.</summary>
    public int GateAsks => Volatile.Read(ref _gateAsks);

    /// <summary>Whether the gate reported a waiting line on the run.</summary>
    public bool ReportedWaiting => Events.Events.OfType<AgentSmith.Contracts.Events.L1StepDetailEvent>().Any();

    private int _gateAsks;

    /// <summary>An agent on one shared deployment, at <paramref name="limit"/> runs at a time.</summary>
    public static AgentConfig Agent(int? limit = 1, string deployment = "gpt41") => new()
    {
        Type = "probe", Model = "gpt-4.1", Deployment = deployment, Endpoint = "https://shared.openai.azure.com",
        MaxConcurrentRuns = limit,
    };

    /// <summary>One model call through a fresh client of <paramref name="task"/>, as a master or sub-agent makes it.</summary>
    public Task<ChatResponse> CallAsync(
        AgentConfig agent, CancellationToken cancellationToken = default,
        AgentSmith.Contracts.Providers.TaskType task = AgentSmith.Contracts.Providers.TaskType.Primary) =>
        Factory.Create(agent, task)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "hello")], cancellationToken: cancellationToken);

    private sealed class CountingSlots(IModelRunSlots inner, ModelGateFixture owner) : IModelRunSlots
    {
        public Task<bool> AcquireAsync(string key, int limit, string runId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref owner._gateAsks);
            return inner.AcquireAsync(key, limit, runId, cancellationToken);
        }

        public Task ReleaseAsync(string runId, CancellationToken cancellationToken) => inner.ReleaseAsync(runId, cancellationToken);

        public Task WaitStartedAsync(string runId, CancellationToken cancellationToken) => inner.WaitStartedAsync(runId, cancellationToken);

        public Task WaitEndedAsync(string runId, CancellationToken cancellationToken) => inner.WaitEndedAsync(runId, cancellationToken);

        public Task<IReadOnlyDictionary<string, TimeSpan>> WaitedAsync(
            IReadOnlyCollection<string> runIds, CancellationToken cancellationToken) => inner.WaitedAsync(runIds, cancellationToken);
    }

    private sealed class Builder(IChatClient provider) : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["probe"];
        public bool AcceptsImageAfterToolResult => true;
        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => provider;
    }
}

/// <summary>The provider: counts the calls inside it and holds each until <see cref="Hold"/> completes.</summary>
internal sealed class ProbeProvider : IChatClient
{
    private int _inside;
    private int _arrived;

    public Task Hold { get; set; } = Task.CompletedTask;
    public int Arrived => Volatile.Read(ref _arrived);
    public int MostInsideAtOnce { get; private set; }
    public TaskCompletionSource FourthArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource FirstArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var inside = Interlocked.Increment(ref _inside);
        lock (this) MostInsideAtOnce = Math.Max(MostInsideAtOnce, inside);
        var arrived = Interlocked.Increment(ref _arrived);
        FirstArrived.TrySetResult();
        if (arrived == 4) FourthArrived.TrySetResult();
        try
        {
            await Hold.WaitAsync(cancellationToken);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"));
        }
        finally
        {
            Interlocked.Decrement(ref _inside);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
