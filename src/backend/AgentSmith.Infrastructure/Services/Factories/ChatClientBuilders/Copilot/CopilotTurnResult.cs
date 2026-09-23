using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders.Copilot;

/// <summary>2026-09-23-4722a: what one model call produced — text, deltas, the tools it asked for,
/// and what it consumed.</summary>
internal sealed record CopilotTurnResult(
    string SessionId,
    string Text,
    IReadOnlyList<string> Deltas,
    IReadOnlyList<CopilotSessionEvent.ExternalToolRequested> ToolRequests,
    CopilotUsage Usage)
{
    /// <summary>The answer, plus one function call per tool the model asked for.</summary>
    internal IList<AIContent> Contents()
    {
        var contents = new List<AIContent>();
        if (!string.IsNullOrEmpty(Text)) contents.Add(new TextContent(Text));
        foreach (var request in ToolRequests)
            contents.Add(new FunctionCallContent(
                request.ToolCallId, request.ToolName, CopilotToolArguments.Parse(request.ArgumentsJson)));
        return contents;
    }
}
