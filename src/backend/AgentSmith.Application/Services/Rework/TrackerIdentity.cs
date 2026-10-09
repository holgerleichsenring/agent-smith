using System.Collections.Concurrent;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Rework;

/// <summary>
/// 2026-10-08-2123: who each tracker's token is, read once per tracker and kept — the webhook's
/// own-actor check and the run's history read ask the same question. A tracker that cannot say is
/// asked again next time rather than cached as nobody.
/// </summary>
public sealed class TrackerIdentity(ITicketProviderFactory tickets, ILogger<TrackerIdentity> logger)
{
    private readonly ConcurrentDictionary<string, TrackerActor> _known = new(StringComparer.Ordinal);

    public async Task<TrackerActor?> SelfAsync(TrackerConnection tracker, CancellationToken cancellationToken)
    {
        if (_known.TryGetValue(tracker.Name, out var known)) return known;
        try
        {
            if (tickets.Create(tracker) is not ITrackerSelf self || await self.SelfAsync(cancellationToken) is not { } actor) return null;
            return _known[tracker.Name] = actor;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not read who the token of tracker {Tracker} is", tracker.Name);
            return null;
        }
    }
}
