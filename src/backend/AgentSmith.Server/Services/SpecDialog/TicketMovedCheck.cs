using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: whether the ticket a conversation was grounded on has CHANGED since it was
/// read. Extracted from <see cref="TicketTextForConversation"/>, which had four lines under the
/// per-file limit and needed them to stop a refused comment read from ungrounding a conversation.
/// <para>
/// It is the only tracker round trip a turn of a bound conversation makes: the text itself comes
/// from the store. Worth knowing wherever the cost of opening a conversation is being counted.
/// </para>
/// </summary>
public sealed class TicketMovedCheck(
    ITicketProviderFactory providers, ILogger<TicketMovedCheck> logger)
{
    public async Task<bool> ForAsync(
        SpecDialogTicketText held, ResolvedProject? project, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(held);
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
}
