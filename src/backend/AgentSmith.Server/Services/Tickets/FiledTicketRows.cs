using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-14-dbcen: the tickets filed here that have no code run on record — one row per approved series
/// carrying a project whose (tracker connection, ticket key) no code run resolves to. The record of the
/// filing is the content: its goal is the title and its flag tells a ticket text from a filed spec. Held
/// (blocked) by what <see cref="UnmetTicketRequirements"/> says, done when a landing of either kind is stored.
/// </summary>
public sealed class FiledTicketRows(
    IServiceScopeFactory scopes, TicketProjects projects, TicketKeyResolver keys,
    UnmetTicketRequirements requirements, ITicketLandingStore landings, ITicketClosedMarkStore marks,
    ILogger<FiledTicketRows> logger)
{
    private sealed record Filing(string Tracker, string TicketKey, string TicketId, string Project, string ContentJson, DateTimeOffset ApprovedAt);

    public Task<IReadOnlyList<TicketSummaryView>> ListAsync(ProjectScope project, CancellationToken ct) =>
        ReadAsync(rows => rows.Within(project), null, ct);

    /// <summary>The pair's never-run row, or null when the pair has a code run or no filing record.</summary>
    public async Task<TicketSummaryView?> OneAsync(string project, string ticketId, CancellationToken ct)
    {
        if (projects.Of(project) is not { } resolved) return null;
        var key = TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var tracker = resolved.Tracker.Name;
        var rows = await ReadAsync(r => r.Where(a => a.Tracker == tracker && a.TicketKey == key), ticketId, ct);
        return rows.Count > 0 ? rows[0] : null;
    }

    private async Task<IReadOnlyList<TicketSummaryView>> ReadAsync(
        Func<IQueryable<ApprovedSeries>, IQueryable<ApprovedSeries>> filter, string? ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var filings = await filter(uow.Set<ApprovedSeries>().AsNoTracking().Where(a => a.Project != ""))
            .OrderBy(a => a.Id)
            .Select(a => new Filing(a.Tracker, a.TicketKey, a.TicketId, a.Project, a.ContentJson, a.ApprovedAt))
            .ToListAsync(ct);
        if (filings.Count == 0) return [];
        var runs = uow.Set<Run>().AsNoTracking().Where(r => r.Pipeline == PipelinePresets.CodeName && r.TicketId != "");
        if (ticketId is not null) runs = runs.Where(r => r.TicketId == ticketId);
        var resolved = new Dictionary<string, ResolvedProject?>(StringComparer.Ordinal);
        ResolvedProject? Of(string project) =>
            resolved.TryGetValue(project, out var known) ? known : resolved[project] = projects.Of(project);
        var ran = (await runs.Select(r => new { r.Project, r.Platform, r.TicketId }).Distinct().ToListAsync(ct))
            .Select(r => (Of(r.Project)?.Tracker.Name, keys.For(r.Project, r.Platform, r.TicketId))).ToHashSet();
        var titles = new Dictionary<(string, string), string?>();
        var records = new List<(Filing Filing, SpecApprovalRecord Record)>();
        foreach (var filing in filings)
        {
            var record = SpecApprovalJson.Read(filing.ContentJson);
            if (record is null) { logger.LogWarning("The approval record of {Key} on {Tracker} could not be read; it is not listed", filing.TicketKey, filing.Tracker); continue; }
            titles[(filing.Tracker, filing.TicketKey)] = record.Set.Goal;
            if (!ran.Contains((filing.Tracker, filing.TicketKey))) records.Add((filing, record));
        }
        var landed = await TicketNodeStates.LandedAsync(landings, records.Select(r => (r.Filing.Tracker, r.Filing.TicketKey)), ct);
        var closed = await TicketNodeStates.ClosedAsync(marks, records.Select(r => (r.Filing.Tracker, r.Filing.TicketKey)), ct);
        var rows = new List<TicketSummaryView>(records.Count);
        foreach (var (filing, record) in records)
        {
            var unmet = (await requirements.OfAsync(filing.Tracker, filing.TicketKey, ct)).Unmet;
            var type = Of(filing.Project)?.Tracker.Type;
            string Name(string id) => type is { } t ? FiledTicketKey.Of(t, id) : id;
            var waits = unmet.Select(e => new TicketWaitsForView(e.RequiredTicketId, filing.Project, Name(e.RequiredTicketId),
                titles.GetValueOrDefault((e.Tracker, e.RequiredTicketKey)) ?? Name(e.RequiredTicketId))).ToList();
            rows.Add(Compose(filing, record, landed.GetValueOrDefault((filing.Tracker, filing.TicketKey)), waits,
                type?.ToString().ToLowerInvariant(), record.Set.Goal ?? Name(filing.TicketId),
                closed.GetValueOrDefault((filing.Tracker, filing.TicketKey))));
        }
        return rows;
    }

    // A landing makes it done; else a close mark closes it (a never-run ticket has no attempt or entry to bring it back).
    private static TicketSummaryView Compose(Filing filing, SpecApprovalRecord record, TicketLandingFact? landing,
        IReadOnlyList<TicketWaitsForView> waits, string? platform, string title, TicketClosedMark? closed)
    {
        var bucket = landing is not null ? TicketBucket.Done : closed is not null ? TicketBucket.Closed
            : waits.Count > 0 ? TicketBucket.Blocked : TicketBucket.Filed;
        var byHand = bucket == TicketBucket.Closed ? closed : null;
        return new TicketSummaryView(filing.Project, filing.TicketId, filing.TicketKey, platform, title, bucket,
            byHand is null ? null : TicketClosedKinds.ByHand, landing?.LandedAt ?? byHand?.ClosedAt ?? filing.ApprovedAt,
            0, 0m, string.Empty, string.Empty, null, null, null, 0, 0, [], [],
            WaitsFor: waits, TicketText: record.TicketText, Closed: TicketClosedView.Of(closed));
    }
}
