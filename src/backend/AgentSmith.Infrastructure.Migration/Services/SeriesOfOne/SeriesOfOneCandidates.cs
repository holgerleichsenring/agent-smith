using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Migration.Services.SeriesOfOne;

/// <summary>
/// 2026-11-25-96aea: the standalone tickets in flight when the series of one arrives — no active series holds them and they
/// have live work: an open pull request, a code run not finished (queued or running, not cancelled) or waiting for input, or a
/// queued RequirementsMet start on a ticket no code run worked. 2026-11-26-a12ea: requirement rows alone are no live work — a
/// filed ticket nobody started stays out, as the old server left it parked; only the queued start is its master's to start.
/// </summary>
public static class SeriesOfOneCandidates
{
    // The value a RequirementsMet nudge is stored with, and the claim-token prefix it keeps once a worker claimed it.
    private static readonly int RequirementsMet = (int)ReworkNudgeOrigin.RequirementsMet;
    private static readonly string ClaimedRequirementsMet = $"{RequirementsMet}:";

    public static async Task<IReadOnlyList<SeriesOfOneCandidate>> ReadAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var (held, stopped) = await HeldAsync(db, ct);
        var runs = await db.Set<Run>().AsNoTracking().Where(r => r.Pipeline == Contracts.Commands.PipelinePresets.CodeName && r.TicketId != "")
            .Select(r => new { r.Project, r.TicketId, r.TicketTitle, r.Status, r.FinishedAt, r.StartedAt, r.CancelRequested }).ToListAsync(ct);
        var open = await db.Set<PrReviewRecord>().AsNoTracking().Where(r => r.State == PrReviewStates.Open)
            .Select(r => new { r.Project, r.TicketId }).ToListAsync(ct);
        var queued = await db.Set<ReworkNudge>().AsNoTracking()
            .Where(n => n.Origin == RequirementsMet || (n.ClaimToken != null && n.ClaimToken.StartsWith(ClaimedRequirementsMet)))
            .Select(n => new { n.Project, n.TicketId }).ToListAsync(ct);
        var worked = runs.Select(r => (r.Project, r.TicketId)).ToHashSet();
        var starts = queued.Select(q => (q.Project, q.TicketId)).Where(k => !worked.Contains(k)).ToHashSet();
        var keys = open.Select(o => (o.Project, o.TicketId))
            .Concat(runs.Where(r => !r.CancelRequested && (r.FinishedAt is null || r.Status == RunStatuses.WaitingForInput))
                .Select(r => (r.Project, r.TicketId)))
            .Concat(starts).Where(k => !held.Contains(k) && !stopped.Contains(k)).Distinct().ToList();
        return [.. keys.Select(k => new SeriesOfOneCandidate(k.Item1, k.Item2,
            runs.Where(r => r.Project == k.Item1 && r.TicketId == k.Item2).OrderByDescending(r => r.StartedAt).FirstOrDefault()?.TicketTitle,
            starts.Contains(k)))];
    }

    // Every ticket an active series holds; the record is one JSON document per series.
    // A ticket whose newest series was stopped stays unled: a stop is a decision.
    private static async Task<(HashSet<(string, string)> Held, HashSet<(string, string)> Stopped)> HeldAsync(
        AgentSmithDbContext db, CancellationToken ct)
    {
        var rows = (await db.Set<SeriesRun>().AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.State, s.ContentJson }).ToListAsync(ct))
            .Select(r => (r.State, Record: System.Text.Json.JsonSerializer.Deserialize<SeriesRunRecord>(r.ContentJson))).Where(r => r.Record is not null).ToList();
        HashSet<(string, string)> Of(IEnumerable<SeriesRunRecord> records) => [.. records.SelectMany(r => r.Tickets.Select(t => (r.Project, t.TicketId)))];
        var newest = rows.SelectMany(r => r.Record!.Tickets.Select(t => (Key: (r.Record.Project, t.TicketId), r.State)))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().State);
        return (Of(rows.Where(r => SeriesRunStates.IsActive(r.State)).Select(r => r.Record!)),
            [.. newest.Where(n => n.Value == SeriesRunStates.Stopped).Select(n => n.Key)]);
    }
}
