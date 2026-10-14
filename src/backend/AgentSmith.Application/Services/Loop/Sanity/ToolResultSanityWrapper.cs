using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// Wraps every function of a tool list in a <see cref="SanityAnnotatedTool"/> that reads its scope
/// from the run's pipeline at call time. Each tool keeps its name, description and schema.
/// </summary>
public sealed class ToolResultSanityWrapper(ToolResultSanity sanity, ToolResultSanityScopeReader reader)
{
    public List<AITool> Wrap(IEnumerable<AITool> tools, PipelineContext pipeline) =>
        [.. tools.Select(tool => tool is AIFunction function
            ? new SanityAnnotatedTool(function, sanity, () => reader.Read(pipeline))
            : tool)];
}
