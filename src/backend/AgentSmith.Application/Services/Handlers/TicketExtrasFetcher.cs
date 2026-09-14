using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-7d9f: the ticket's optional companions — p0317's comment thread, its
/// text-like documents, and the full attachment listing that lets a prompt name a binary
/// it can never inline. Each is fetched FAIL-SOFT: a run without the conversation beats
/// no run, and the providers say so at their own call sites.
/// <para>
/// Extracted out of FetchTicketHandler, which sits at its file-length ratchet and had to
/// make room for a fourth thing read with the ticket (the epic ground). Three
/// near-identical try/catch blocks were also one rule written three times.
/// </para>
/// </summary>
public sealed class TicketExtrasFetcher(ILogger<TicketExtrasFetcher> logger)
{
    public async Task FetchAsync(
        ITicketProvider provider, TicketId ticketId, PipelineContext pipeline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(pipeline);

        await SetAsync(ContextKeys.TicketComments, "comment(s)", ticketId, pipeline,
            () => provider.GetCommentsAsync(ticketId, cancellationToken));
        await SetAsync(ContextKeys.TicketDocuments, "document attachment(s)", ticketId, pipeline,
            () => provider.DownloadDocumentAttachmentsAsync(ticketId, cancellationToken));
        await SetAsync(ContextKeys.TicketAttachmentRefs, "attachment reference(s)", ticketId,
            pipeline, () => provider.GetAttachmentRefsAsync(ticketId, cancellationToken));
    }

    // An empty result sets nothing: every reader treats an absent key as "none", and a
    // present-but-empty list would make a ticket with no comments render a comment section.
    private async Task SetAsync<T>(
        string key, string what, TicketId ticketId, PipelineContext pipeline,
        Func<Task<IReadOnlyList<T>>> fetch)
    {
        try
        {
            var items = await fetch();
            if (items.Count == 0) return;
            pipeline.Set(key, items);
            logger.LogInformation(
                "Fetched {Count} {What} from ticket {TicketId}", items.Count, what, ticketId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to fetch {What} for ticket {TicketId} — continuing without them",
                what, ticketId);
        }
    }
}
