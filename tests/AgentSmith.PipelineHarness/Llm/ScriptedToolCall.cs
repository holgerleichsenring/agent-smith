namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0199: record of one FunctionCallContent the scripted client emitted.
/// Tests assert on (Name, Arguments) — the behavioural contract — instead
/// of the master's prose. Pinning prose breaks on every prompt iteration.
/// </summary>
public sealed record ScriptedToolCall(
    string CallId,
    string Name,
    IDictionary<string, object?> Arguments);
