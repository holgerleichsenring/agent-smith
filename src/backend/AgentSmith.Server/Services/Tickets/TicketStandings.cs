using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
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
/// 2026-10-15-114fb: and the close marks, through the same (tracker, key) projection.
/// </summary>
public sealed class TicketStandings(
    TicketCheckStore checks, IServiceScopeFactory scopes, ITicketLandingStore landings,
    ITicketClosedMarkStore closed, IConfigurationLoader configLoader, ServerContext serverContext)
{
    public async Task<TicketStanding> ReadAsync(IReadOnlyList<Run> lastAttempts, CancellationToken ct)
    {
        var held = await checks.ForAsync([.. lastAttempts.Where(r => r.Status == RunStatuses.Failed).Select(r => r.Id)], ct);
        var candidates = lastAttempts.Where(r => held.GetValueOrDefault(r.Id) is { FingerprintUnchanged: true, Acts.Count: 0 }).ToList();
        var keyed = Keyed(lastAttempts);
        var landed = await ByHeadAsync(keyed, (tracker, keys) => landings.LandedAsync(tracker, keys, ct), l => l.TicketKey);
        return new(held,
            await HandbacksAsync([.. lastAttempts.Where(r => r.Status == RunStatuses.WaitingForInput).Select(r => r.Id)], ct),
            await PrRecordsAsync(candidates, ct))
        {
            Landed = landed.ToDictionary(p => p.Key, p => p.Value.LandedAt),
            Closed = await ByHeadAsync(keyed, (tracker, keys) => closed.OfAsync(tracker, keys, ct), m => m.TicketKey),
        };
    }

    // Each head by (tracker connection, ticket key) from the configuration loaded once; landings and marks share it.
    private List<(Run Run, string Tracker, string Key)> Keyed(IReadOnlyList<Run> heads)
    {
        var projects = configLoader.LoadConfig(serverContext.ConfigPath).Projects;
        return [.. heads.Where(r => projects.ContainsKey(r.Project)).Select(r => (Run: r, Tracker: projects[r.Project].Tracker.Name,
            Key: TicketKey.For(projects[r.Project].Tracker.Type.ToString().ToLowerInvariant(), r.TicketId).Value))];
    }

    private static async Task<Dictionary<(string, string), TFact>> ByHeadAsync<TFact>(List<(Run Run, string Tracker, string Key)> keyed,
        Func<string, string[], Task<IReadOnlyList<TFact>>> read, Func<TFact, string> keyOf)
    {
        var found = new Dictionary<(string, string), TFact>();
        foreach (var tracker in keyed.GroupBy(h => h.Tracker, StringComparer.Ordinal))
            foreach (var fact in await read(tracker.Key, [.. tracker.Select(h => h.Key).Distinct()]))
                foreach (var head in tracker.Where(h => h.Key == keyOf(fact))) found[(head.Run.Project, head.Run.TicketId)] = fact;
        return found;
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
