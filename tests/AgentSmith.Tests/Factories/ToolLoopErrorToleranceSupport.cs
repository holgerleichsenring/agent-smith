using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

/// <summary>The production chat-client factory over a provider that calls one tool, then answers.</summary>
internal static class ToolLoopErrorToleranceSupport
{
    public const string Answer = "the step after the failures";

    public static AgentConfig Agent() => new() { Type = "scripted", Model = "m", Models = new ModelRegistryConfig() };

    public static ChatClientFactory LiveFactory(IChatClient provider) => new(
        [new ScriptedBuilder(provider)],
        EventTestStubs.NoOp,
        EventTestStubs.RunContext,
        new ModelPricingResolver(),
        new AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry(
            NullLogger<AgentSmith.Infrastructure.Services.RateLimiting.LlmRateLimiterRegistry>.Instance),
        new AgentSmith.Infrastructure.Services.RateLimiting.ThrottleWaitReporter(),
        new AgentSmith.Contracts.Runs.NullRunTraceWriter(),
        TurnActivityRecorder.Silent(),
        new CompactionSummaryRequest(),
        new WindowDerivedCompaction(),
        new AgentSmith.Infrastructure.Services.ToolImages.ToolImageRelay(new(), new()),
        new AgentSmith.Contracts.Runs.SecretMasker(new AgentSmith.Contracts.Services.LoadedSecretValues(AgentSmith.Contracts.Models.Configuration.AgentSmithConfig.Empty())),
        NullLoggerFactory.Instance);

    /// <summary>A read_file tool that runs <paramref name="body"/> on every call.</summary>
    public static AITool Tool(Func<string, string> body) => AIFunctionFactory.Create((string path) => body(path), "read_file");

    private sealed class ScriptedBuilder(IChatClient provider) : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = ["scripted"];
        public bool AcceptsImageAfterToolResult => true;
        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => provider;
    }
}

/// <summary>Calls read_file <c>calls</c> times, then answers; keeps every request it received.</summary>
internal sealed class ToolCallingProvider(int calls) : IChatClient
{
    private int _turn;

    public List<List<ChatMessage>> Requests { get; } = [];

    public IEnumerable<FunctionResultContent> ResultsSeen =>
        Requests.Last().SelectMany(m => m.Contents).OfType<FunctionResultContent>();

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default)
    {
        Requests.Add(messages.ToList());
        var reply = _turn++ < calls
            ? new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent($"call-{_turn}", "read_file",
                    new Dictionary<string, object?> { ["path"] = "/etc/shadow" })])
            : new ChatMessage(ChatRole.Assistant, ToolLoopErrorToleranceSupport.Answer);
        return Task.FromResult(new ChatResponse(reply));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken ct = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
