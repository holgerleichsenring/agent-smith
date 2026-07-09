using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Fetches a ticket from the configured provider, including image attachments,
/// and publishes p0184's TicketFetchedEvent so the runs-page card can show the
/// human-readable title and the Fetch-ticket step body can render details on
/// drill-in.
/// </summary>
public sealed class FetchTicketHandler(
    ITicketProviderFactory factory,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
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
        var attachmentCount = 0;
        try
        {
            var images = await provider.DownloadImageAttachmentsAsync(
                context.TicketId, cancellationToken);

            if (images.Count > 0)
            {
                context.Pipeline.Set(ContextKeys.Attachments,
                    (IReadOnlyList<TicketImageAttachment>)images);
                attachmentCount = images.Count;
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

        // p0317: fetch the comment thread — the conversation is part of the
        // requirement record. Fail-soft: a run without comments beats no run.
        await FetchCommentsAsync(provider, context, cancellationToken);

        // p0317: fetch text-like documents (materialized into the run-record dir
        // at AgenticMaster time, once a sandbox exists) + the full ref list so
        // the prompt can name non-viewable binaries. Both fail-soft.
        await FetchDocumentsAsync(provider, context, cancellationToken);
        await FetchAttachmentRefsAsync(provider, context, cancellationToken);

        // p0184: publish the typed event for the dashboard. Best-effort —
        // a publish failure must not break ticket fetch.
        var runId = runContext.CurrentRunId;
        if (!string.IsNullOrEmpty(runId))
        {
            try
            {
                await eventPublisher.PublishAsync(new TicketFetchedEvent(
                    RunId: runId,
                    TicketId: ticket.Id.Value,
                    Title: ticket.Title,
                    Description: ticket.Description,
                    State: ticket.Status,
                    Labels: ticket.Labels,
                    AttachmentCount: attachmentCount,
                    Source: ticket.Source,
                    Timestamp: DateTimeOffset.UtcNow),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex,
                    "Failed to publish TicketFetchedEvent for {TicketId}", context.TicketId);
            }
        }

        return CommandResult.Ok($"Ticket {context.TicketId} fetched from {provider.ProviderType}");
    }

    private async Task FetchCommentsAsync(
        ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        try
        {
            var comments = await provider.GetCommentsAsync(context.TicketId, cancellationToken);
            if (comments.Count == 0) return;
            context.Pipeline.Set(ContextKeys.TicketComments, comments);
            logger.LogInformation(
                "Fetched {Count} comment(s) from ticket {TicketId}",
                comments.Count, context.TicketId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to fetch comments for ticket {TicketId}, continuing without the conversation",
                context.TicketId);
        }
    }

    private async Task FetchDocumentsAsync(
        ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        try
        {
            var documents = await provider.DownloadDocumentAttachmentsAsync(
                context.TicketId, cancellationToken);
            if (documents.Count == 0) return;
            context.Pipeline.Set(ContextKeys.TicketDocuments, documents);
            logger.LogInformation(
                "Downloaded {Count} document attachment(s) from ticket {TicketId}",
                documents.Count, context.TicketId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to download document attachments for ticket {TicketId}, continuing without documents",
                context.TicketId);
        }
    }

    private async Task FetchAttachmentRefsAsync(
        ITicketProvider provider, FetchTicketContext context, CancellationToken cancellationToken)
    {
        try
        {
            var refs = await provider.GetAttachmentRefsAsync(context.TicketId, cancellationToken);
            if (refs.Count == 0) return;
            context.Pipeline.Set(ContextKeys.TicketAttachmentRefs, refs);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to list attachment refs for ticket {TicketId}, continuing without the listing",
                context.TicketId);
        }
    }
}
