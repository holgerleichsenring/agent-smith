using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands;

/// <summary>
/// Resolves and executes command handlers via DI.
/// Central place for logging and error handling.
/// </summary>
public sealed class CommandExecutor(
    IServiceProvider serviceProvider,
    ILogger<CommandExecutor> logger) : ICommandExecutor
{
    public async Task<CommandResult> ExecuteAsync<TContext>(
        TContext context,
        CancellationToken cancellationToken = default)
        where TContext : ICommandContext
    {
        var contextName = typeof(TContext).Name;
        logger.LogInformation("Executing {Command}...", contextName);

        var handler = serviceProvider.GetService<ICommandHandler<TContext>>();
        if (handler is null)
            return CommandResult.Fail($"No handler registered for {contextName}");

        return await ExecuteHandler(handler, context, contextName, cancellationToken);
    }

    private async Task<CommandResult> ExecuteHandler<TContext>(
        ICommandHandler<TContext> handler,
        TContext context,
        string contextName,
        CancellationToken cancellationToken)
        where TContext : ICommandContext
    {
        try
        {
            var result = await handler.ExecuteAsync(context, cancellationToken);
            LogResult(contextName, result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler {Command} failed", contextName);
            return CommandResult.Fail(ex.Message, ex);
        }
    }

    private void LogResult(string contextName, CommandResult result)
    {
        if (result.Success)
            logger.LogInformation("{Command} completed: {Message}", contextName, result.Message);
        else
            logger.LogWarning("{Command} failed: {Message}", contextName, result.Message);
    }
}
