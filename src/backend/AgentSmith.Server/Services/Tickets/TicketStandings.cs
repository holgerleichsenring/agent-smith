using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Checks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59h: what the ticket rows read beside their runs — the stored between-attempts check of each
/// failed last attempt, and the newest hand-back row of each handed-back one, whose words and time the row
/// carries. A ticket the check saw unchanged and untouched also reads its pull requests' full records — the
/// list reads only their standing decisions, and a person's later comment there takes it out of stuck. Read
/// from the store only; the tracker is never asked here. 2026-10-14-dbcei: landings, by the configuration loaded once.
/// </summary>
public sealed class TicketStandings(
    TicketCheckStore checks, IServiceScopeFactory scopes, ITicketLandingStore landings,
    IConfigurationLoader configLoader, ServerContext serverContext)
{
    public async Task<TicketStanding> ReadAsync(IReadOnlyList<Run> lastAttempts, CancellationToken ct)
    {
        var held = await checks.ForAsync([.. lastAttempts.Where(r => r.Status == RunStatuses.Failed).Select(r => r.Id)], ct);
        var candidates = lastAttempts.Where(r => held.GetValueOrDefault(r.Id) is { FingerprintUnchanged: true, Acts.Count: 0 }).ToList();
        return new(held,
            await HandbacksAsync([.. lastAttempts.Where(r => r.Status == RunStatuses.WaitingForInput).Select(r => r.Id)], ct),
            await PrRecordsAsync(candidates, ct)) { Landed = await LandedAsync(lastAttempts, ct) };
    }

    private async Task<IReadOnlyDictionary<(string, string), DateTimeOffset>> LandedAsync(IReadOnlyList<Run> heads, CancellationToken ct)
    {
        var landed = new Dictionary<(string, string), DateTimeOffset>();
        var projects = configLoader.LoadConfig(serverContext.ConfigPath).Projects;
        var keyed = heads.Where(r => projects.ContainsKey(r.Project)).Select(r => (Run: r, Tracker: projects[r.Project].Tracker.Name,
            Key: TicketKey.For(projects[r.Project].Tracker.Type.ToString().ToLowerInvariant(), r.TicketId).Value));
        foreach (var tracker in keyed.GroupBy(h => h.Tracker, StringComparer.Ordinal))
            foreach (var landing in await landings.LandedAsync(tracker.Key, [.. tracker.Select(h => h.Key).Distinct()], ct))
                foreach (var head in tracker.Where(h => h.Key == landing.TicketKey)) landed[(head.Run.Project, head.Run.TicketId)] = landing.LandedAt;
        return landed;
    }

    private async Task<IReadOnlyDictionary<(string, string), IReadOnlyList<PrReviewRecordView>>> PrRecordsAsync(
        IReadOnlyList<Run> candidates, CancellationToken ct)
    {
        var records = new Dictionary<(string, string), IReadOnlyList<PrReviewRecordView>>();
        if (candidates.Count == 0) return records;
        using var scope = scopes.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IPrReviewRecordReader>();
        foreach (var run in candidates) records[(run.Project, run.TicketId)] = await reader.ByTicketAsync(run.Project, run.TicketId, ct);
        return records;
    }

    private async Task<IReadOnlyDictionary<string, TicketHandbackView>> HandbacksAsync(IReadOnlyList<string> runIds, CancellationToken ct)
    {
        if (runIds.Count == 0) return new Dictionary<string, TicketHandbackView>();
        using var scope = scopes.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<RunConversationEntry>().AsNoTracking()
            .Where(e => runIds.Contains(e.RunId) && e.Kind == RunConversationKinds.HandBack).ToListAsync(ct);
        return rows.GroupBy(e => e.RunId, StringComparer.Ordinal).ToDictionary(
            g => g.Key, g => Handback(g.OrderBy(e => e.EventSeq).ThenBy(e => e.Id).Last()), StringComparer.Ordinal);
    }

    private static TicketHandbackView Handback(RunConversationEntry row) => new(row.Text ?? string.Empty, row.At);
}

/// <summary>2026-10-13-9a59h: the checks and hand-backs read for a set of last attempts.</summary>
public sealed record TicketStanding(
    IReadOnlyDictionary<string, TicketBetweenAttemptsState> Checks,
    IReadOnlyDictionary<string, TicketHandbackView> Handbacks,
    IReadOnlyDictionary<(string Project, string TicketId), IReadOnlyList<PrReviewRecordView>> PrRecords)
{
    /// <summary>2026-10-14-dbcei: when each last attempt's ticket landed, merged or by hand; absent when it did not.</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), DateTimeOffset> Landed { get; init; } =
        new Dictionary<(string, string), DateTimeOffset>();

    public TicketBetweenAttemptsState? CheckOf(string runId) => Checks.GetValueOrDefault(runId);
    public DateTimeOffset? LandedAt(Run last) => Landed.TryGetValue((last.Project, last.TicketId), out var at) ? at : null;

    /// <summary>The full pull-request records of a ticket the check saw unchanged; empty otherwise.</summary>
    public IReadOnlyList<PrReviewRecordView> PrRecordsOf(Run last) => PrRecords.GetValueOrDefault((last.Project, last.TicketId)) ?? [];

    /// <summary>A hand-back row takes its since and its words from the recorded hand-back.</summary>
    public TicketSummaryView Apply(TicketSummaryView summary) =>
        summary.Kind == TicketNeedsYouKind.Handback && Handbacks.GetValueOrDefault(summary.NewestRunId) is { } handback
            ? summary with { Since = handback.At, Handback = handback }
            : summary;
}
