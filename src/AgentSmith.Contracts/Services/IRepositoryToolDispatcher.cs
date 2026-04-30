using System.Text.Json.Nodes;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Runs a named repository tool (list_files / read_file / grep / write_file …)
/// against a given repository path. Implemented in Infrastructure as a thin
/// wrapper over ToolExecutor; consumed by Application services that need
/// tool-driven repo inspection without taking a direct Infrastructure dependency.
/// </summary>
public interface IRepositoryToolDispatcher
{
    Task<string> ExecuteAsync(
        string repositoryPath,
        string toolName,
        JsonNode? input,
        CancellationToken cancellationToken);
}
