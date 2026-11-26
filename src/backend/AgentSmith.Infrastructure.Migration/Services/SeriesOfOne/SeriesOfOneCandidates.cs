using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Migration.Services.SeriesOfOne;

/// <summary>
/// 2026-11-25-96aea: the standalone tickets in flight when the series of one arrives — no active series holds them and they
/// have an open pull request, an unfinished or waiting code run, a queued RequirementsMet start, or requirement rows and no
/// code run yet. A queued start, or requirement rows without a run, marks the ticket as one whose master decides its start.
/// </summary>
public static class SeriesOfOneCandidates
{
    // The value a RequirementsMet nudge is stored with.
    private static readonly int RequirementsMet = (int)ReworkNudgeOrigin.RequirementsMet;

    public static async Task<IReadOnlyList<SeriesOfOneCandidate>> ReadAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var (held, filed, stopped) = await HeldAsync(db, ct);
        var runs = await db.Set<Run>().AsNoTracking().Where(r => r.Pipeline == Contracts.Commands.PipelinePresets.CodeName && r.TicketId != "")
            .Select(r => new { r.Project, r.TicketId, r.TicketTitle, r.Status, r.FinishedAt, r.StartedAt }).ToListAsync(ct);
        var open = await db.Set<PrReviewRecord>().AsNoTracking().Where(r => r.State == PrReviewStates.Open)
            .Select(r => new { r.Project, r.TicketId }).ToListAsync(ct);
        var queued = await db.Set<ReworkNudge>().AsNoTracking().Where(n => n.Origin == RequirementsMet)
            .Select(n => new { n.Project, n.TicketId }).ToListAsync(ct);
        var waiting = await db.Set<TicketRequirement>().AsNoTracking().Select(e => new { e.Project, e.TicketId }).ToListAsync(ct);
        var worked = runs.Select(r => (r.Project, r.TicketId)).ToHashSet();
        var keys = open.Select(o => (o.Project, o.TicketId))
            .Concat(runs.Where(r => r.FinishedAt is null || r.Status == RunStatuses.WaitingForInput).Select(r => (r.Project, r.TicketId)))
            .Concat(queued.Select(q => (q.Project, q.TicketId))).Concat(waiting.Select(w => (w.Project, w.TicketId)).Where(k => !worked.Contains(k) && !filed.Contains(k)))
            .Where(k => !held.Contains(k) && !stopped.Contains(k)).Distinct().ToList();
        var starts = queued.Select(q => (q.Project, q.TicketId)).Concat(waiting.Select(w => (w.Project, w.TicketId))).ToHashSet();
        return [.. keys.Select(k => new SeriesOfOneCandidate(k.Item1, k.Item2,
            runs.Where(r => r.Project == k.Item1 && r.TicketId == k.Item2).OrderByDescending(r => r.StartedAt).FirstOrDefault()?.TicketTitle,
            starts.Contains(k) && !worked.Contains(k)))];
    }

    // Every ticket an active series holds, and every ticket any series ever held (a waiting ticket of a series that ended is
    // that series' business, not revived here); the record is one JSON document per series.
    // A ticket whose newest series was stopped stays unled: a stop is a decision.
    private static async Task<(HashSet<(string, string)> Held, HashSet<(string, string)> Filed, HashSet<(string, string)> Stopped)> HeldAsync(
        AgentSmithDbContext db, CancellationToken ct)
    {
        var rows = (await db.Set<SeriesRun>().AsNoTracking().OrderBy(s => s.Id).Select(s => new { s.State, s.ContentJson }).ToListAsync(ct))
            .Select(r => (r.State, Record: System.Text.Json.JsonSerializer.Deserialize<SeriesRunRecord>(r.ContentJson))).Where(r => r.Record is not null).ToList();
        HashSet<(string, string)> Of(IEnumerable<SeriesRunRecord> records) => [.. records.SelectMany(r => r.Tickets.Select(t => (r.Project, t.TicketId)))];
        var newest = rows.SelectMany(r => r.Record!.Tickets.Select(t => (Key: (r.Record.Project, t.TicketId), r.State)))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Last().State);
        return (Of(rows.Where(r => SeriesRunStates.IsActive(r.State)).Select(r => r.Record!)), Of(rows.Select(r => r.Record!)),
            [.. newest.Where(n => n.Value == SeriesRunStates.Stopped).Select(n => n.Key)]);
    }
}
