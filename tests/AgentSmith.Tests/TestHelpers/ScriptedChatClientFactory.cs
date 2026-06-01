using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0196: IChatClientFactory that hands out a stub IChatClient returning
/// "{}" (parseable empty object / no tool calls). Ends agentic loops
/// immediately and gives structured handlers a benign default. Per-preset
/// tests can replace the default response when a specific handler needs
/// richer content to not throw on parse.
/// </summary>
internal sealed class ScriptedChatClientFactory : IChatClientFactory
{
    private readonly Func<ChatResponse> _responder;
    public List<int> ResponseCounter { get; } = new();

    public ScriptedChatClientFactory(Func<ChatResponse>? responder = null)
    {
        _responder = responder ?? DefaultResponder;
    }

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) =>
        new Inner(_responder, ResponseCounter);

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
    public string GetModel(AgentConfig agent, TaskType task) => "stub-model";

    private static ChatResponse DefaultResponder() =>
        new(new ChatMessage(ChatRole.Assistant, "{}"));

    private sealed class Inner(Func<ChatResponse> responder, List<int> counter) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            counter.Add(counter.Count);
            return Task.FromResult(responder());
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
