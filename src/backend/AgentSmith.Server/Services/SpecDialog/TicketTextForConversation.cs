using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51c: what a design conversation knows its ticket says — read once when the
/// conversation is bound, kept with it, and re-read on demand.
/// <para>
/// KEPT rather than fetched per turn, because a seed lives for one turn and a conversation lives
/// for days: a reply three days later must be grounded on the same text the earlier turns
/// discussed. The fingerprint is of the ticket as the TRACKER stored it, so a later turn can SAY
/// the ticket moved instead of the operator having to notice.
/// </para>
/// <para>
/// Our own note and our own comments never travel: the note is recognised by its marker pair and
/// our comments by their heading, and feeding either back shows the model its own echo.
/// </para>
/// </summary>
public sealed class TicketTextForConversation(
    ITicketProviderFactory providers,
    SpecDialogTicketTextRepository store,
    ApprovedSetDivergence divergence,
    TimeProvider timeProvider,
    ILogger<TicketTextForConversation> logger)
{
    /// <summary>Reads the ticket and stores what the conversation may be grounded on.</summary>
    public async Task<SeededTicket?> ReadAsync(
        string sessionId, ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var provider = providers.Create(project.Tracker);
            var ticket = await provider.GetTicketAsync(new TicketId(ticketId), ct);
            var comments = provider.SupportsComments
                ? await provider.GetCommentsAsync(ticket.Id, ct)
                : [];
            var seeded = TicketTextComposer.Compose(ticket, comments);
            await store.SaveAsync(
                new Infrastructure.Persistence.Entities.SpecDialogTicketText
                {
                    SessionId = sessionId,
                    TicketId = ticket.Id.Value,
                    Title = Trimmed(ticket.Title),
                    Text = seeded.Text,
                    Truncated = seeded.Truncated,
                    Fingerprint = TicketTextFingerprint.Of(ticket),
                    ReadAt = timeProvider.GetUtcNow(),
                }, ct);
            return new SeededTicket(
                Trimmed(ticket.Title), seeded.Text, seeded.Truncated, TicketTextFingerprint.Of(ticket));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "Ticket {Ticket} could not be read for conversation {Session}; the conversation "
                + "continues ungrounded in it", ticketId, sessionId);
            return null;
        }
    }

    /// <summary>
    /// What this conversation was grounded on, or null when it holds none — and whether the ticket
    /// has MOVED since. Computed from the fingerprint, never asked of the operator: there is
    /// already a fingerprint of a ticket's text, built to make exactly this visible.
    /// <para>
    /// 2026-09-25-8e51e: and where the ticket and the specification somebody approved for it
    /// DISAGREE, computed here for the same reason — a turn told to compare two texts it was not
    /// given would invent the comparison.
    /// </para>
    /// </summary>
    public async Task<SeededTicket?> HeldAsync(
        string sessionId, ResolvedProject? project, CancellationToken ct)
    {
        if (await store.GetAsync(sessionId, ct) is not { } held) return null;
        return new SeededTicket(
            held.Title, held.Text, held.Truncated, held.Fingerprint,
            await MovedAsync(held, project, ct),
            await divergence.ForAsync(project, held.TicketId, held.Text, ct));
    }

    private async Task<bool> MovedAsync(
        Infrastructure.Persistence.Entities.SpecDialogTicketText held,
        ResolvedProject? project, CancellationToken ct)
    {
        if (project is null) return false;
        try
        {
            var ticket = await providers.Create(project.Tracker)
                .GetTicketAsync(new TicketId(held.TicketId), ct);
            return !string.Equals(
                TicketTextFingerprint.Of(ticket), held.Fingerprint, StringComparison.Ordinal);
        }
        // A tracker this turn could not reach says nothing about whether the ticket moved.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Could not tell whether ticket {Ticket} has moved", held.TicketId);
            return false;
        }
    }

    private static string Trimmed(string title) =>
        title.Length <= PersistenceLimits.ConversationSubject
            ? title
            : title[..PersistenceLimits.ConversationSubject];
}
