using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
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
/// from the store only; the tracker is never asked here.
/// </summary>
public sealed class TicketStandings(TicketCheckStore checks, IServiceScopeFactory scopes)
{
    public async Task<TicketStanding> ReadAsync(IReadOnlyList<Run> lastAttempts, CancellationToken ct)
    {
        var held = await checks.ForAsync([.. lastAttempts.Where(r => r.Status == RunStatuses.Failed).Select(r => r.Id)], ct);
        var candidates = lastAttempts.Where(r => held.GetValueOrDefault(r.Id) is { FingerprintUnchanged: true, Acts.Count: 0 }).ToList();
        return new(held,
            await HandbacksAsync([.. lastAttempts.Where(r => r.Status == RunStatuses.WaitingForInput).Select(r => r.Id)], ct),
            await PrRecordsAsync(candidates, ct));
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
    public TicketBetweenAttemptsState? CheckOf(string runId) => Checks.GetValueOrDefault(runId);

    /// <summary>The full pull-request records of a ticket the check saw unchanged; empty otherwise.</summary>
    public IReadOnlyList<PrReviewRecordView> PrRecordsOf(Run last) => PrRecords.GetValueOrDefault((last.Project, last.TicketId)) ?? [];

    /// <summary>A hand-back row takes its since and its words from the recorded hand-back.</summary>
    public TicketSummaryView Apply(TicketSummaryView summary) =>
        summary.Kind == TicketNeedsYouKind.Handback && Handbacks.GetValueOrDefault(summary.NewestRunId) is { } handback
            ? summary with { Since = handback.At, Handback = handback }
            : summary;
}
