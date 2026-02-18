using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

/// <summary>
/// Fetches a ticket from the configured provider.
/// </summary>
public sealed class FetchTicketHandler(
    ITicketProviderFactory factory,
    ILogger<FetchTicketHandler> logger)
    : ICommandHandler<FetchTicketContext>
{
    public async Task<CommandResult> ExecuteAsync(
        FetchTicketContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching ticket {TicketId}...", context.TicketId);

        var provider = factory.Create(context.Config);
        var ticket = await provider.GetTicketAsync(context.TicketId, cancellationToken);

        context.Pipeline.Set(ContextKeys.Ticket, ticket);
        return CommandResult.Ok($"Ticket {context.TicketId} fetched from {provider.ProviderType}");
    }
}
