using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Handles a specific command context type.
/// Each handler implements exactly one ICommandContext.
/// Resolved via DI by the CommandExecutor.
/// </summary>
public interface ICommandHandler<in TContext> where TContext : ICommandContext
{
    Task<CommandResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
}
