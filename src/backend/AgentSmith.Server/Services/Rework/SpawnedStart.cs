using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Series;

namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-11-26-5f6e: a start's outcome once the 'start' run made the ticket claimable — read from what the poller's
/// route (<see cref="ReworkFallbackSpawn"/>) answered: a claimed run is started, a queued one queued, anything else refused
/// with the claim's words.</summary>
internal static class SpawnedStart
{
    // What the poller's route did once the ticket was claimable: a claimed run, a queued one, or nothing and why.
    public static SeriesStart Of(string ticketId, string made, SpawnResult? spawned, string? nowhere)
    {
        if (nowhere is not null)
            return SeriesStart.Refused($"Ticket {ticketId} was not started: {made.TrimEnd('.')}, but nothing routes it now — {nowhere}. "
                + "It starts once the project's trigger routes it.");
        var claims = spawned?.ClaimResults ?? [];
        var claim = claims.FirstOrDefault(c => c.Outcome == ClaimOutcome.Claimed) ?? claims.FirstOrDefault(c => c.Outcome == ClaimOutcome.Queued)
            ?? claims.FirstOrDefault();
        return claim?.Outcome switch
        {
            ClaimOutcome.Claimed => new(SeriesStartKind.Started, $"Ticket {ticketId} started: run {spawned!.RunId}.", spawned.RunId),
            ClaimOutcome.Queued => new(SeriesStartKind.Queued,
                $"Ticket {ticketId} is queued as run {spawned!.RunId}: {claim.Error ?? "it waits for room"}; it starts once there is room.",
                spawned.RunId),
            ClaimOutcome.AlreadyClaimed => SeriesStart.Refused($"Ticket {ticketId} was not started: a run holds it already; retry or tell it instead."),
            ClaimOutcome.Rejected => SeriesStart.Refused($"Ticket {ticketId} was not started: the claim refused it ({claim.Rejection}): "
                + $"{claim.Error ?? "no reason given"}"),
            ClaimOutcome.Failed => SeriesStart.Refused($"Ticket {ticketId} was not started: the claim failed: {claim.Error}; start_ticket tries again."),
            _ => SeriesStart.Refused($"Ticket {ticketId} was not started: the spawn answered no claim; start_ticket tries again."),
        };
    }
}
