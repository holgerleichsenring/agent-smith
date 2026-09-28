using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: the ticket one conversation is bound to, read in slices.
/// <para>
/// Built per turn by the runner, which is the only place the PROJECT and the TICKET ID are both
/// resolved — the stored row carries the id and no tracker, so a reader that took only a session
/// id could not reach a board at all.
/// </para>
/// </summary>
public sealed class BoundTicketReader(
    ITicketProviderFactory providers, ResolvedProject project, string ticketId,
    ILogger logger) : ITicketReader
{
    /// <summary>What one call may return. The transcript carries a tool's answer for the rest of
    /// the turn, so an unbounded read would pay the cap's cost back with interest.</summary>
    internal const int Slice = 8_000;

    public async Task<TicketReadSlice> ReadAsync(int from, CancellationToken cancellationToken)
    {
        var start = Math.Max(0, from);
        try
        {
            var provider = providers.Create(project.Tracker);
            var ticket = await provider.GetTicketAsync(new TicketId(ticketId), cancellationToken);
            var whole = TicketTextComposer.Whole(
                ticket, await CommentsAsync(provider, ticket.Id, cancellationToken));
            if (start >= whole.Length) return new TicketReadSlice(string.Empty, start, 0, null);
            var text = whole[start..Math.Min(whole.Length, start + Slice)];
            return new TicketReadSlice(text, start, whole.Length - start - text.Length, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Ticket} could not be re-read", ticketId);
            return TicketReadSlice.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private async Task<IReadOnlyList<Domain.Entities.TicketComment>> CommentsAsync(
        ITicketProvider provider, TicketId id, CancellationToken ct)
    {
        if (!provider.SupportsComments) return [];
        try
        {
            return await provider.GetCommentsAsync(id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "The discussion on ticket {Ticket} could not be re-read", ticketId);
            return [];
        }
    }
}
