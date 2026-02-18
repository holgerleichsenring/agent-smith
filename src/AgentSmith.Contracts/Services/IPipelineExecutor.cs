using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Orchestrates a pipeline: builds ICommandContext records from command names,
/// dispatches them through the CommandExecutor, stops on first failure.
/// </summary>
public interface IPipelineExecutor
{
    Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}
