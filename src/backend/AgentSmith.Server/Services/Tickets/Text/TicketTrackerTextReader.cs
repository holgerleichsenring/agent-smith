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

    public async Task<TicketTrackerRead> ReadAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var key = $"ticket-text:{project.Name}/{ticketId}";
        if (cache.TryGetValue<TicketTrackerRead>(key, out var cached) && cached is not null) return cached;
        var read = await LoadAsync(project, ticketId, ct);
        cache.Set(key, read, CachedFor);
        return read;
    }

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
