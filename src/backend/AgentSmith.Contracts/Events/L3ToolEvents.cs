namespace AgentSmith.Contracts.Events;

/// <summary>
/// LLM tool invocation start. <see cref="ArgsLength"/> stays metadata-only
/// for the raw arg blob (may carry source code, file content, secrets).
/// <see cref="Summary"/> is an optional, producer-curated one-liner
/// (≤120 chars) extracted from a whitelist of operator-visible argument
/// keys (path, file, url, …) so the activity row reads "read_file
/// src/Foo.cs" instead of "read_file (47B)". Softens the strict
/// metadata-only boundary from p0169e — see decisions/p0175.yaml.
/// </summary>
public sealed record ToolCallEvent(
    string RunId,
    string Tool,
    int ArgsLength,
    DateTimeOffset Timestamp,
    string? Summary = null)
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
    DateTimeOffset Timestamp,
    string? ErrorMessage = null)
    : RunEvent(RunId, EventType.ToolResult, Timestamp);
