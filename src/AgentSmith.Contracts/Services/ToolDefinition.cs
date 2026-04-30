using System.Text.Json.Nodes;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Provider-neutral declaration of a tool an agentic analyzer can call.
/// Each adapter translates this into its native SDK shape (Anthropic Tool,
/// OpenAI ChatTool, Gemini FunctionDeclaration).
/// </summary>
public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonObject InputSchema);
