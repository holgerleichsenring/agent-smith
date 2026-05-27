namespace AgentSmith.Contracts.Events;

/// <summary>
/// LLM tool invocation start. <see cref="ArgsLength"/> is metadata only — the
/// arg blob may carry source code, file paths, or read-back content; same
/// security class as prompts, kept out of the event stream by design.
/// </summary>
public sealed record ToolCallEvent(
    string RunId,
    string Tool,
    int ArgsLength,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.ToolCall, Timestamp);

/// <summary>
/// LLM tool invocation result. <see cref="ResultLength"/> is metadata only —
/// the result blob may carry file content; same boundary as <see cref="ToolCallEvent"/>.
/// </summary>
public sealed record ToolResultEvent(
    string RunId,
    string Tool,
    bool Ok,
    int ResultLength,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.ToolResult, Timestamp);
