using System.Collections.Concurrent;
using System.Reflection;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Resolves and executes command handlers via DI.
/// Supports both generic (compile-time) and non-generic (runtime) dispatch.
/// </summary>
public sealed class CommandExecutor(
    IServiceProvider serviceProvider,
    ILogger<CommandExecutor> logger) : ICommandExecutor
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> DispatchCache = new();

    private static readonly MethodInfo OpenGenericMethod =
        typeof(CommandExecutor).GetMethods()
            .First(m => m.Name == nameof(ExecuteAsync) && m.IsGenericMethod);

    public async Task<CommandResult> ExecuteAsync<TContext>(
        TContext context,
        CancellationToken cancellationToken)
        where TContext : ICommandContext
    {
        var contextName = typeof(TContext).Name;
        logger.LogInformation("Executing {Command}...", contextName);

        var handler = serviceProvider.GetService<ICommandHandler<TContext>>();
        if (handler is null)
            return CommandResult.Fail($"No handler registered for {contextName}");

        return await ExecuteHandler(handler, context, contextName, cancellationToken);
    }

    public Task<CommandResult> ExecuteAsync(
        ICommandContext context,
        CancellationToken cancellationToken)
    {
        var contextType = context.GetType();
        var method = DispatchCache.GetOrAdd(contextType,
            static type => OpenGenericMethod.MakeGenericMethod(type));

        return (Task<CommandResult>)method.Invoke(this, [context, cancellationToken])!;
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
        if (result.IsSuccess)
            logger.LogInformation("{Command} completed: {Message}", contextName, result.Message);
        else
            logger.LogWarning("{Command} failed: {Message}", contextName, result.Message);
    }
}
