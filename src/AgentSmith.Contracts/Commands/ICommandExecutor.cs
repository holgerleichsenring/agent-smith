using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Resolves and executes the matching ICommandHandler for a given ICommandContext.
/// Central place for cross-cutting concerns (logging, error handling).
/// </summary>
public interface ICommandExecutor
{
    Task<CommandResult> ExecuteAsync<TContext>(
        TContext context,
        CancellationToken cancellationToken = default)
        where TContext : ICommandContext;
}
