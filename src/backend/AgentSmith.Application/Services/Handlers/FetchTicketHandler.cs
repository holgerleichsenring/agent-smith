using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
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
        // p0326: an inline ticket (the demo's trackerless path) IS the requirement
        // record — materialize it directly, no provider lookup. Extends the p0322a
        // null-TicketId seam: inline runs carry no TicketId either.
        if (context.Pipeline.TryGet<InlineTicket>(ContextKeys.InlineTicket, out var inline)
            && inline is not null)
            return await FetchInlineAsync(inline, context.Pipeline, cancellationToken);

        // p0322a: init-project runs FetchTicket too, and a CLI-triggered init
        // carries no ticket — skip cleanly instead of failing the step.
        if (context.TicketId is null)
        {
            logger.LogInformation("Run has no ticket - skipping ticket fetch.");
            return CommandResult.Ok("No ticket on this run - fetch skipped");
        }

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
        await FetchCommentsAsync(provider, context.TicketId, context.Pipeline, cancellationToken);

        // p0317: fetch text-like documents (materialized into the run-record dir
        // at AgenticMaster time, once a sandbox exists) + the full ref list so
        // the prompt can name non-viewable binaries. Both fail-soft.
        await FetchDocumentsAsync(provider, context.TicketId, context.Pipeline, cancellationToken);
        await FetchAttachmentRefsAsync(provider, context.TicketId, context.Pipeline, cancellationToken);

        await PublishFetchedEventAsync(ticket, attachmentCount, cancellationToken);

        return CommandResult.Ok($"Ticket {context.TicketId} fetched from {provider.ProviderType}");
    }

    // p0326: the inline payload becomes the run's Ticket without any provider —
    // the demo's whole point is proving the production path with zero tracker setup.
    private async Task<CommandResult> FetchInlineAsync(
        InlineTicket inline, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var ticket = inline.ToTicket();
        pipeline.Set(ContextKeys.Ticket, ticket);
        logger.LogInformation(
            "Inline ticket materialized: {Title} — provider lookup skipped", ticket.Title);
        await PublishFetchedEventAsync(ticket, attachmentCount: 0, cancellationToken);
        return CommandResult.Ok($"Inline ticket '{ticket.Title}' materialized (no tracker involved)");
    }

    // p0184: publish the typed event for the dashboard. Best-effort —
    // a publish failure must not break ticket fetch.
    private async Task PublishFetchedEventAsync(
        Ticket ticket, int attachmentCount, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId)) return;
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
                "Failed to publish TicketFetchedEvent for {TicketId}", ticket.Id);
        }
    }

    private async Task FetchCommentsAsync(
        ITicketProvider provider, TicketId ticketId, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            var comments = await provider.GetCommentsAsync(ticketId, cancellationToken);
            if (comments.Count == 0) return;
            pipeline.Set(ContextKeys.TicketComments, comments);
            logger.LogInformation(
                "Fetched {Count} comment(s) from ticket {TicketId}",
                comments.Count, ticketId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to fetch comments for ticket {TicketId}, continuing without the conversation",
                ticketId);
        }
    }

    private async Task FetchDocumentsAsync(
        ITicketProvider provider, TicketId ticketId, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            var documents = await provider.DownloadDocumentAttachmentsAsync(
                ticketId, cancellationToken);
            if (documents.Count == 0) return;
            pipeline.Set(ContextKeys.TicketDocuments, documents);
            logger.LogInformation(
                "Downloaded {Count} document attachment(s) from ticket {TicketId}",
                documents.Count, ticketId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to download document attachments for ticket {TicketId}, continuing without documents",
                ticketId);
        }
    }

    private async Task FetchAttachmentRefsAsync(
        ITicketProvider provider, TicketId ticketId, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        try
        {
            var refs = await provider.GetAttachmentRefsAsync(ticketId, cancellationToken);
            if (refs.Count == 0) return;
            pipeline.Set(ContextKeys.TicketAttachmentRefs, refs);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to list attachment refs for ticket {TicketId}, continuing without the listing",
                ticketId);
        }
    }
}
