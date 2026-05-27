using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Fetches a ticket from the configured provider, including image attachments.
/// </summary>
public sealed class FetchTicketHandler(
    ITicketProviderFactory factory,
    ILogger<FetchTicketHandler> logger)
    : ICommandHandler<FetchTicketContext>
{
    public async Task<CommandResult> ExecuteAsync(
        FetchTicketContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching ticket {TicketId}...", context.TicketId);

        var provider = factory.Create(context.Config);
        var ticket = await provider.GetTicketAsync(context.TicketId, cancellationToken);
        context.Pipeline.Set(ContextKeys.Ticket, ticket);

        // p87: Download image attachments for LLM vision input
        try
        {
            var images = await provider.DownloadImageAttachmentsAsync(
                context.TicketId, cancellationToken);

            if (images.Count > 0)
            {
                context.Pipeline.Set(ContextKeys.Attachments,
                    (IReadOnlyList<TicketImageAttachment>)images);
                logger.LogInformation(
                    "Downloaded {Count} image attachment(s) from ticket {TicketId}",
                    images.Count, context.TicketId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to download attachments for ticket {TicketId}, continuing without images",
                context.TicketId);
        }

        return CommandResult.Ok($"Ticket {context.TicketId} fetched from {provider.ProviderType}");
    }
}
