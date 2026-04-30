namespace AgentSmith.Contracts.Services;

/// <summary>
/// Result of executing a single ToolCall, fed back to the model on the next
/// iteration. Id must match the originating ToolCall.Id.
/// </summary>
public sealed record ToolResult(
    string Id,
    string Content,
    bool IsError = false);
