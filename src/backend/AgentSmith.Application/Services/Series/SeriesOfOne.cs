using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>
/// 2026-11-25-96aea: a standalone ticket is a series of one — opened when its code run starts, a filing of one files it, or a
/// pull request of it is read that no series holds — so its series master leads it like every other ticket. It is opened only
/// while no active series holds the ticket and its newest series was not stopped (a stop is a person's or the master's
/// decision, never undone by a read); its principal is the project's machine identity (the run started under the project's
/// trigger) and its conversation the ticket's own when one is bound, else none. Its id derives from the ticket and the series
/// before it (<see cref="SeriesRunIds.OneOf"/>), so of two openers racing exactly one opens it.
/// </summary>
public sealed class SeriesOfOne(ISeriesRunStore store, SeriesMembership membership, ISeriesConversation conversation, TimeProvider clock)
{
    /// <summary>Opens the series of one and answers its id; null when a series holds the ticket or its last one was stopped.</summary>
    public async Task<string?> OpenAsync(ResolvedProject project, string ticketId, string? title, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (await store.HoldsTicketAsync(project.Name, ticketId, ct)) return null;
        var before = await store.ForTicketAsync(project.Name, ticketId, ct);
        if (before is { State: SeriesRunStates.Stopped }) return null;
        var talk = await conversation.TicketConversationAsync(project, ticketId, ct) ?? string.Empty;
        var key = RequiresUnmetGate.Named(ticketId);
        var ticket = new SeriesTicket(ticketId, key, string.IsNullOrWhiteSpace(title) ? key : title.Trim(), false, clock.GetUtcNow());
        try { return await membership.OpenOneAsync(project, ticket, talk, project.ActorName, ct, id: SeriesRunIds.OneOf(project.Name, ticketId, before?.Id)); }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            if (await store.HoldsTicketAsync(project.Name, ticketId, ct)) return null; // the other opener's insert of the same id won
            throw;
        }
    }
}
