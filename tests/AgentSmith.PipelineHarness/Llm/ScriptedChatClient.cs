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
    private readonly ScopeClassificationScript _scopeScript = new();
    private readonly SpecAttackScript _specAttackScript = new(); // 2026-10-13-8762g
    private int _toolCallCounter;

    public int InvocationCount { get; private set; }
    /// <summary>2026-09-17-042eh: private. A preset assertion wants the last call the preset
    /// DROVE, and steps served from their own slots (scope classification, spec review) run
    /// after it — see <see cref="LastScriptedMessages"/>, which every reader now goes through.</summary>
    private IReadOnlyList<ChatMessage> LastMessages { get; set; } = Array.Empty<ChatMessage>();
    /// <summary>2026-09-08-805f: the message list of EVERY call, in order — what the
    /// governor injected into a running pass is visible only here.
    /// <para>2026-09-13-84c0 wanted the same thing for a different question — which ROUND was
    /// handed what — and found this already here. <see cref="PromptsSeen"/> is the joined
    /// text of each call, derived rather than recorded a second time.</para></summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> CallMessages => _callMessages;
    private readonly List<IReadOnlyList<ChatMessage>> _callMessages = new();

    /// <summary>
    /// 2026-09-17-042eh: the most recent call that is not the SPEC REVIEW — which runs after
    /// everything a preset scripts, so "the last call" stopped being the last call a preset
    /// drove. The scope classification is not excluded: it is a PRELUDE, so a preset whose
    /// only call is that one still reads it here, exactly as it did before the review existed.
    /// </summary>
    /// <summary>2026-10-13-8762i: the calls the script answered — every call but the framework's own spec-review
    /// and attack-review calls, which a design turn now makes after its proposal.</summary>
    public int ScriptedInvocationCount =>
        _callMessages.Count(m => !SpecAttackScript.Answers(m));

    public IReadOnlyList<ChatMessage> LastScriptedMessages =>
        _callMessages.LastOrDefault(m => !SpecAttackScript.Answers(m))
        ?? Array.Empty<ChatMessage>();

    /// <summary>Each call's messages joined to one string, in order.</summary>
    public IReadOnlyList<string> PromptsSeen =>
        [.. _callMessages.Select(messages => string.Join("\n", messages.Select(m => m.Text)))];
    /// <summary>The options of the most recent call — which tools, if any, it carried.</summary>
    public ChatOptions? LastOptions { get; private set; }
    /// <summary>2026-11-10-14a3h: the options of the first call — the tool list a turn opens with.</summary>
    public ChatOptions? FirstOptions { get; private set; }
    public IReadOnlyList<ScriptedToolCall> ToolCalls => _toolCalls;

    public ScriptedChatClient EnqueueText(string text)
    {
        _responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));
        return this;
    }

    /// <summary>
    /// 2026-11-18-98fa: scripts how a spec's coding master ends — its markdown report and the finish_spec call in one
    /// answer, then the empty answer that closes the loop after the tool result.
    /// </summary>
    public ScriptedChatClient EnqueueFinish(
        string status = "green", string summary = "done", string report = "## Delivered\n- the change, built and tested",
        IReadOnlyList<(string Goal, string Why)>? followUps = null, string[]? failingTests = null, string? blocker = null,
        string[]? ignoredInstructions = null)
    {
        var args = new Dictionary<string, object?> { ["status"] = status, ["summary"] = summary };
        if (followUps is not null)
            args["follow_ups"] = System.Text.Json.JsonSerializer.SerializeToElement(followUps.Select(f => new { goal = f.Goal, why = f.Why }));
        if (failingTests is not null) args["failing_tests"] = System.Text.Json.JsonSerializer.SerializeToElement(failingTests);
        if (blocker is not null) args["blocker"] = blocker;
        if (ignoredInstructions is not null)
            args["ignored_instructions"] = System.Text.Json.JsonSerializer.SerializeToElement(ignoredInstructions);
        // Not on ToolCalls: those pin the order of the work tools a test drives; how a master ends is this helper's.
        var callId = "call_" + (++_toolCallCounter);
        _responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant,
            [new TextContent(report), new FunctionCallContent(callId, "finish_spec", args)])));
        _responses.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty)));
        return this;
    }

    /// <summary>
    /// p0413a: scripts the ScopeRepos classification call, which draws from its own
    /// slot (see <see cref="ScopeClassificationScript"/>) instead of the FIFO.
    /// </summary>
    public ScriptedChatClient EnqueueScopeReply(string text)
    {
        _scopeScript.Enqueue(text);
        return this;
    }

    /// <summary>2026-11-02-2b26d: scripts an attack reviewer's answer, which draws from its own slot
    /// (see <see cref="SpecAttackScript"/>) instead of the FIFO; one per reviewer call reached.</summary>
    public ScriptedChatClient EnqueueSpecAttackReply(string text)
    {
        _specAttackScript.Enqueue(text);
        return this;
    }

    /// <summary>2026-11-02-2b26d: scripts an attack reviewer's tool call — the look its finding cites.</summary>
    public ScriptedChatClient EnqueueSpecAttackToolCall(string toolName, string arguments)
    {
        var call = new FunctionCallContent("attack_" + Interlocked.Increment(ref _toolCallCounter), toolName, ParseArgs(arguments));
        _specAttackScript.Enqueue(new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
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

    /// <summary>
    /// Scripts the NEXT call to run a callback at invocation time then yield its
    /// result (or throw). Unlike <see cref="EnqueueThrow"/> (which is evaluated at
    /// enqueue time), the callback runs WHEN the LLM call happens — used to model
    /// an operator cancel that fires DURING the master's in-flight call (cancel
    /// the run token, then throw OperationCanceledException), which the pre-cancel
    /// path can't reach (the loop short-circuits before calling).
    /// </summary>
    public ScriptedChatClient EnqueueDeferred(Func<ChatResponse> callback)
    {
        _responses.Enqueue(callback);
        return this;
    }

    /// <summary>2026-11-18-c8e2d: each sub-agent's own script, by the name its system prompt opens with ("You are
    /// {name}."), so a child working beside its master never draws from the master's FIFO.</summary>
    private readonly Dictionary<string, Queue<ChatResponse>> _agents = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>2026-11-18-c8e2d: scripts a sub-agent's next tool call.</summary>
    public ScriptedChatClient EnqueueForToolCall(string agent, string toolName, string arguments)
    {
        var call = new FunctionCallContent("agent_" + Interlocked.Increment(ref _toolCallCounter), toolName, ParseArgs(arguments));
        return EnqueueFor(agent, new ChatResponse(new ChatMessage(ChatRole.Assistant, [call])));
    }

    /// <summary>2026-11-18-c8e2d: scripts a sub-agent's next answer.</summary>
    public ScriptedChatClient EnqueueForText(string agent, string text) =>
        EnqueueFor(agent, new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

    /// <summary>Every call a sub-agent made, its whole message list each.</summary>
    public IReadOnlyList<IReadOnlyList<ChatMessage>> AgentCalls(string agent)
    {
        lock (_gate)
            return [.. _callMessages.Where(m => AgentOf(m) == agent)];
    }

    private ScriptedChatClient EnqueueFor(string agent, ChatResponse response)
    {
        lock (_gate) (_agents.TryGetValue(agent, out var q) ? q : _agents[agent] = new Queue<ChatResponse>()).Enqueue(response);
        return this;
    }

    private string? AgentOf(IReadOnlyList<ChatMessage> messages) =>
        messages.FirstOrDefault(m => m.Role == ChatRole.System)?.Text is { } system
            ? _agents.Keys.FirstOrDefault(name => system.StartsWith($"You are {name}.", StringComparison.Ordinal))
            : null;

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        lock (_gate) return Respond(messages, options);
    }

    private Task<ChatResponse> Respond(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        InvocationCount++;
        LastMessages = messages.ToList();
        _callMessages.Add(LastMessages);
        LastOptions = options;
        FirstOptions ??= options;
        if (AgentOf(LastMessages) is { } agent)
            return Task.FromResult(_agents[agent].Count > 0 ? _agents[agent].Dequeue() : DefaultEmpty());
        if (ScopeClassificationScript.Answers(LastMessages))
            return Task.FromResult(_scopeScript.Next());
        if (SpecAttackScript.Answers(LastMessages))
            return Task.FromResult(_specAttackScript.Next());
        if (_responses.Count == 0) return Task.FromResult(DefaultEmpty());
        var next = _responses.Dequeue();
        if (next is Func<ChatResponse> deferred)
        {
            try { return Task.FromResult(deferred()); }
            catch (Exception dex) { return Task.FromException<ChatResponse>(dex); }
        }
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
