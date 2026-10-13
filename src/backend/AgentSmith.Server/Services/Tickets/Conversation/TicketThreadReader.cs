using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Conversation;

/// <summary>
/// 2026-10-13-9a59h: a ticket's live comment thread, read through a per-replica cache entry per (project,
/// ticket) for sixty seconds — a conversation refetched on every RunsChanged reads the tracker at most once
/// a minute per replica, with no shared state. A failed read is cached as unavailable for the same minute,
/// so a tracker that is down is not asked again on every refetch either.
/// </summary>
public sealed class TicketThreadReader(
    ITicketProviderFactory providers, IMemoryCache cache, ILogger<TicketThreadReader> logger)
{
    public static readonly TimeSpan CachedFor = TimeSpan.FromSeconds(60);

    /// <summary>The thread, or null when the tracker could not be read.</summary>
    public async Task<IReadOnlyList<TicketComment>?> ReadAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var key = $"ticket-thread:{project.Name}/{ticketId}";
        if (cache.TryGetValue<ThreadRead>(key, out var cached) && cached is not null) return cached.Comments;
        var read = new ThreadRead(await LoadAsync(project, ticketId, ct));
        cache.Set(key, read, CachedFor);
        return read.Comments;
    }

    private async Task<IReadOnlyList<TicketComment>?> LoadAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        try
        {
            return await providers.Create(project.Tracker).GetCommentsAsync(new TicketId(ticketId), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The thread of ticket {Ticket} in {Project} could not be read", ticketId, project.Name);
            return null;
        }
    }

    private sealed record ThreadRead(IReadOnlyList<TicketComment>? Comments);
}
