using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Loop;

internal sealed class ScriptedRuntimeChatClient : IChatClient
{
    private readonly Queue<Func<Task<ChatResponse>>> _responses;
    public int CallCount { get; private set; }

    public ScriptedRuntimeChatClient(params Func<ChatResponse>[] responses)
        => _responses = new Queue<Func<Task<ChatResponse>>>(
            responses.Select<Func<ChatResponse>, Func<Task<ChatResponse>>>(r => () => Task.FromResult(r())));

    public static ScriptedRuntimeChatClient Async(params Func<Task<ChatResponse>>[] responses)
        => new(responses);

    private ScriptedRuntimeChatClient(Func<Task<ChatResponse>>[] responses)
        => _responses = new Queue<Func<Task<ChatResponse>>>(responses);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        var produce = _responses.Count > 0 ? _responses.Dequeue() : () => Task.FromResult(Make("{}"));
        return produce();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    public static ChatResponse Make(string text, int input = 10, int output = 5)
        => new(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output }
        };
}

internal sealed class StubRuntimeChatClientFactory : IChatClientFactory
{
    private readonly IChatClient _client;
    public int? LastMaxIterations { get; private set; }

    public StubRuntimeChatClientFactory(IChatClient client) => _client = client;

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, AgentSmith.Contracts.Providers.MasterLoopHooks? masterLoopHooks = null)
    {
        LastMaxIterations = maxIterations;
        return _client;
    }

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 8192;
    public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
}

internal static class RuntimeBuilder
{
    public static (SkillCallRuntime Runtime, PipelineCostTracker Tracker, StubRuntimeChatClientFactory Factory)
        Build(IChatClient chat, LoopLimitsConfig? limits = null)
    {
        var resolvedLimits = limits ?? new LoopLimitsConfig();
        var factory = new StubRuntimeChatClientFactory(chat);
        var tracker = new PipelineCostTracker();
        var gate = new PipelineConcurrencyGate(resolvedLimits);
        var noOp = new NoOpSkillOutputValidator();
        var validatorFactory = new SkillOutputValidatorFactory(noOp, noOp);
        var runtime = new SkillCallRuntime(
            factory, gate, resolvedLimits,
            new OutcomeClassifier(), new RetryCoordinator(),
            validatorFactory,
            new RuntimeObservationFactory(),
            EventTestStubs.NoOp,
            EventTestStubs.RunContext,
            NullLogger<SkillCallRuntime>.Instance);
        return (runtime, tracker, factory);
    }

    public static SkillCallRequest MakeRequest(
        SkillExecutionPhase phase = SkillExecutionPhase.Plan,
        string? investigatorMode = null)
        => new()
        {
            SkillName = "test-skill",
            Role = "planner",
            Phase = phase,
            InvestigatorMode = investigatorMode,
            PromptParts = new List<ChatMessage>
            {
                new(ChatRole.System, "system"),
                new(ChatRole.User, "user")
            },
            ToolSet = Array.Empty<AITool>(),
            AgentConfig = new AgentConfig { Type = "claude", Model = "claude-sonnet-4-20250514" },
            TaskType = TaskType.Primary
        };
}
