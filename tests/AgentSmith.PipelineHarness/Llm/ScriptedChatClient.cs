using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0199: scripted IChatClient. Tests append responses to a FIFO queue;
/// the master skill / structured handlers pull from it. Empty queue
/// returns "{}" (benign default — ends agentic loops, parses as empty
/// object for structured-output handlers).
///
/// Tool-call SHAPE assertions read <see cref="ToolCalls"/> (every
/// FunctionCallContent we emitted, in order). Tests must assert which
/// tool was called with which args — never the literal assistant text.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    // Holds ChatResponse (a scripted reply) or Exception (a scripted failure,
    // e.g. an LLM-layer timeout) so tests can drive the failure paths too.
    private readonly Queue<object> _responses = new();
    private readonly List<ScriptedToolCall> _toolCalls = new();
    private int _toolCallCounter;

    public int InvocationCount { get; private set; }
    public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = Array.Empty<ChatMessage>();
    public IReadOnlyList<ScriptedToolCall> ToolCalls => _toolCalls;

    public ScriptedChatClient EnqueueText(string text)
    {
        _responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        return this;
    }

    public ScriptedChatClient EnqueueToolCall(string toolName, string arguments)
    {
        var callId = "call_" + (++_toolCallCounter);
        var args = ParseArgs(arguments);
        _toolCalls.Add(new ScriptedToolCall(callId, toolName, args));
        var call = new FunctionCallContent(callId, toolName, args);
        _responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
        return this;
    }

    /// <summary>
    /// Scripts the NEXT call to throw — used to drive the failure paths
    /// (e.g. an LLM-layer NetworkTimeout surfaces as OperationCanceledException).
    /// </summary>
    public ScriptedChatClient EnqueueThrow(Exception exception)
    {
        _responses.Enqueue(exception);
        return this;
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        LastMessages = messages.ToList();
        if (_responses.Count == 0) return Task.FromResult(DefaultEmpty());
        var next = _responses.Dequeue();
        return next is Exception ex
            ? Task.FromException<ChatResponse>(ex)
            : Task.FromResult((ChatResponse)next);
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
