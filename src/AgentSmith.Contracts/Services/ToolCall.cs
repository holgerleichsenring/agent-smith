using System.Text.Json.Nodes;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// A single tool invocation issued by the model during an agentic loop.
/// Id is provider-assigned; the analyzer round-trips it back as ToolResult.Id.
/// </summary>
public sealed record ToolCall(
    string Id,
    string Name,
    JsonNode? Input);
