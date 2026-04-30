namespace AgentSmith.Contracts.Services;

/// <summary>
/// Caller-supplied executor for tool calls during an agentic-analyzer run.
/// The analyzer doesn't bake in a specific tool registry — consumers wire
/// their own (e.g. read-only file tools for the project analyzer; full
/// repo write tools for plan-execute).
/// </summary>
public interface IToolCallHandler
{
    Task<ToolResult> HandleAsync(ToolCall call, CancellationToken cancellationToken);
}
