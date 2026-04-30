using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services;

/// <summary>
/// IToolCallHandler that allows only read-only repository inspection tools
/// (list_files, read_file, grep). Write attempts (write_file, run_command)
/// are refused with a structured error so the analyst LLM cannot mutate
/// the repo during a project-analysis run.
/// </summary>
public sealed class ReadOnlyToolCallHandler(
    Func<string, JsonNode?, CancellationToken, Task<string>> dispatcher) : IToolCallHandler
{
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "list_files", "read_file", "grep"
    };

    public async Task<ToolResult> HandleAsync(ToolCall call, CancellationToken cancellationToken)
    {
        if (!Allowed.Contains(call.Name))
            return new ToolResult(call.Id,
                $"Error: tool '{call.Name}' is not allowed in analysis mode (read-only).",
                IsError: true);

        var content = await dispatcher(call.Name, call.Input, cancellationToken);
        var isError = content.StartsWith("Error:", StringComparison.Ordinal);
        return new ToolResult(call.Id, content, isError);
    }
}
