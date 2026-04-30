using System.Text.Json.Nodes;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// IRepositoryToolDispatcher implementation: builds a fresh ToolExecutor per
/// invocation rooted at the supplied repository path and delegates the tool
/// name + JSON input to it. Per-invocation construction avoids leaking state
/// between concurrent analyzer runs.
/// </summary>
public sealed class RepositoryToolDispatcher(ILoggerFactory loggerFactory) : IRepositoryToolDispatcher
{
    public async Task<string> ExecuteAsync(
        string repositoryPath, string toolName, JsonNode? input,
        CancellationToken cancellationToken)
    {
        var executor = new ToolExecutor(
            repositoryPath,
            loggerFactory.CreateLogger<RepositoryToolDispatcher>());
        return await executor.ExecuteAsync(toolName, input);
    }
}
