using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// A ticket as its tracker holds it now, read through a per-replica cache entry per (project, ticket) for sixty
/// seconds; a failed read is cached for the same minute. The answer passes through the label-note strip the fetch
/// door applies, so the page never shows our own note.
/// </summary>
public sealed class TicketTrackerTextReader(
    ITicketProviderFactory providers, IMemoryCache cache, TimeProvider clock, ILogger<TicketTrackerTextReader> logger)
{
    public static readonly TimeSpan CachedFor = TimeSpan.FromSeconds(60);

    /// <param name="fresh">2026-11-17-dd05d: past the cache — a person came back from the tracker and must see it now.</param>
    public async Task<TicketTrackerRead> ReadAsync(ResolvedProject project, string ticketId, CancellationToken ct, bool fresh = false)
    {
        var key = Key(project.Name, ticketId);
        if (!fresh && cache.TryGetValue<TicketTrackerRead>(key, out var cached) && cached is not null) return cached;
        var read = await LoadAsync(project, ticketId, ct);
        cache.Set(key, read, CachedFor);
        return read;
    }

    /// <summary>2026-11-17-dd05d: this system just rewrote the ticket, so a cached read of it is stale.</summary>
    public void Forget(string project, string ticketId) => cache.Remove(Key(project, ticketId));

    private static string Key(string project, string ticketId) => $"ticket-text:{project}/{ticketId}";

    private async Task<TicketTrackerRead> LoadAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        try
        {
            var ticket = await providers.Create(project.Tracker).GetTicketAsync(new TicketId(ticketId), ct);
            return new TicketTrackerRead(TicketLabelNoteStripper.Strip(ticket), null, clock.GetUtcNow());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ticket {Ticket} in {Project} could not be read", ticketId, project.Name);
            return new TicketTrackerRead(null, TicketReadFailureReason.Of(ex), clock.GetUtcNow());
        }
    }
}
