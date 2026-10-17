using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Loop.Sanity;

/// <summary>
/// Wraps every function of a tool list in a <see cref="SanityAnnotatedTool"/> that reads its scope
/// from the run's pipeline at call time. Each tool keeps its name, description and schema.
/// 2026-10-17-a196a: inside the annotation, every function passes <see cref="RepositoryScopedWriteTool"/>,
/// which refuses a file write into a repository the current spec does not name.
/// </summary>
public sealed class ToolResultSanityWrapper(
    ToolResultSanity sanity, ToolResultSanityScopeReader reader, UnnamedRepositoryWrite unnamedWrite)
{
    public List<AITool> Wrap(IEnumerable<AITool> tools, PipelineContext pipeline) =>
        [.. tools.Select(tool => tool is AIFunction function
            ? new SanityAnnotatedTool(
                new RepositoryScopedWriteTool(function, unnamedWrite, () => reader.Read(pipeline), pipeline),
                sanity, () => reader.Read(pipeline))
            : tool)];
}
