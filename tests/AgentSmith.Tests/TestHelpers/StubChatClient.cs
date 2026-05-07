using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// Test stub: replays a pre-scripted queue of ChatResponse strings on each
/// GetResponseAsync call. Used by FilterRoundHandler integration tests to
/// drive multi-batch behavior deterministically.
/// </summary>
internal sealed class StubChatClient(Queue<string> responses) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var text = responses.Count > 0 ? responses.Dequeue() : "[]";
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>
/// Test stub IChatClientFactory: hands out a single pre-built StubChatClient.
/// </summary>
internal sealed class StubChatClientFactory(StubChatClient client, int maxOutputTokens = 8192)
    : IChatClientFactory
{
    public IChatClient Create(AgentConfig agent, TaskType task) => client;
    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => maxOutputTokens;
    public string GetModel(AgentConfig agent, TaskType task) => "stub-model";
}
