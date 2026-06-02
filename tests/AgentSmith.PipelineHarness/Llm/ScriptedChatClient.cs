using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0199: scripted IChatClient. Tests append responses to a FIFO queue;
/// the master skill / structured handlers pull from it. Empty queue
/// returns "{}" (benign default — ends agentic loops, parses as empty
/// object for structured-output handlers).
///
/// Two ways to assert behavior:
///   - InvocationCount property → how many times the LLM was called.
///   - LastMessages → the last prompt the master sent (assert SHAPE not
///     literal text, per the phase's review feedback).
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new();
    public int InvocationCount { get; private set; }
    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = Array.Empty<ChatMessage>();

    public ScriptedChatClient EnqueueText(string text)
    {
        _responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        return this;
    }

    public ScriptedChatClient EnqueueToolCall(string toolName, string arguments)
    {
        var call = new FunctionCallContent("call_" + InvocationCount, toolName, ParseArgs(arguments));
        var message = new ChatMessage(ChatRole.Assistant, [call]);
        _responses.Enqueue(new ChatResponse(message));
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        LastMessages = messages.ToList();
        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : DefaultEmpty());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }

    private static ChatResponse DefaultEmpty() =>
        new(new ChatMessage(ChatRole.Assistant, "{}"));

    private static IDictionary<string, object?> ParseArgs(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        return parsed ?? new Dictionary<string, object?>();
    }
}
