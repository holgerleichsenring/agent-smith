using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ToolImages;

/// <summary>
/// 2026-10-01-283dd: a provider that answers each request with the next scripted round of
/// tool calls — then with plain text — and keeps a snapshot of every request it was sent.
/// </summary>
internal sealed class ScriptedToolChat(params string[][] rounds) : IChatClient
{
    private int _next;

    public List<List<ChatMessage>> Requests { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(messages.ToList());
        var round = _next++;
        var reply = round < rounds.Length
            ? new ChatMessage(ChatRole.Assistant,
                [.. rounds[round].Select((tool, i) => (AIContent)new FunctionCallContent($"call_{round}_{i}", tool))])
            : new ChatMessage(ChatRole.Assistant, "finished");
        return Task.FromResult(new ChatResponse(reply));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
