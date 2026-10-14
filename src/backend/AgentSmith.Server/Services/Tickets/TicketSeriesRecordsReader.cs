using AgentSmith.Contracts.Commands;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: reads the series rows of a set of attempts in one scope, one set query per table —
/// flat in the number of tickets and specs, never one query per row.
/// </summary>
public sealed class TicketSeriesRecordsReader(IServiceScopeFactory scopes)
{
    public async Task<TicketSeriesRecords> ReadAsync(IReadOnlyList<string> runIds, bool full, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var cuts = await uow.Set<RunSeriesCut>().AsNoTracking()
            .Where(c => runIds.Contains(c.RunId)).OrderBy(c => c.Id).ToListAsync(ct);
        var specs = await uow.Set<RunSpec>().AsNoTracking()
            .Where(s => runIds.Contains(s.RunId)).OrderBy(s => s.Id).ToListAsync(ct);
        var attempts = await uow.Set<RunAttempt>().AsNoTracking()
            .Where(a => runIds.Contains(a.RunId)).ToDictionaryAsync(a => a.RunId, StringComparer.Ordinal, ct);
        var checkpointed = await uow.Set<RunCheckpoint>().AsNoTracking()
            .Where(c => runIds.Contains(c.RunId)).Select(c => c.RunId).Distinct().ToListAsync(ct);
        if (!full)
            return new(cuts, specs, attempts, new Dictionary<string, string>(), [], checkpointed.ToHashSet(StringComparer.Ordinal));
        return new(cuts, specs, attempts, await GateStepsAsync(uow, runIds, ct),
            await uow.Set<RunCriterionJudgement>().AsNoTracking()
                .Where(j => runIds.Contains(j.RunId)).OrderBy(j => j.Id).ToListAsync(ct),
            checkpointed.ToHashSet(StringComparer.Ordinal),
            await uow.Set<RunLedgerTransition>().AsNoTracking() // 2026-10-13-9a59f
                .Where(t => runIds.Contains(t.RunId) && t.SpecId != null).OrderBy(t => t.Id).ToListAsync(ct),
            await RecordsAsync(uow, runIds, ct));
    }

    // 2026-10-13-8762m: every spec record of the attempts, one set query; the record carries the build note.
    private static async Task<IReadOnlyDictionary<(string RunId, string SpecId), string>> RecordsAsync(
        IUnitOfWork uow, IReadOnlyList<string> runIds, CancellationToken ct)
    {
        var rows = await uow.Set<RunArtifact>().AsNoTracking()
            .Where(a => runIds.Contains(a.RunId) && a.Kind.StartsWith(RunSpecProjection.RecordKindPrefix))
            .OrderBy(a => a.Id).Select(a => new { a.RunId, a.Kind, a.Content }).ToListAsync(ct);
        return rows.GroupBy(a => (a.RunId, a.Kind[RunSpecProjection.RecordKindPrefix.Length..]))
            .ToDictionary(g => g.Key, g => g.Last().Content ?? string.Empty);
    }

    // The newest SpecGate step of each run, by row id: a resumed run gates again.
    private static async Task<IReadOnlyDictionary<string, string>> GateStepsAsync(
        IUnitOfWork uow, IReadOnlyList<string> runIds, CancellationToken ct)
    {
        var steps = await uow.Set<RunStep>().AsNoTracking()
            .Where(s => runIds.Contains(s.RunId) && s.CommandName == CommandNames.SpecGate)
            .OrderBy(s => s.Id).Select(s => new { s.RunId, s.Status }).ToListAsync(ct);
        return steps.GroupBy(s => s.RunId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Status, StringComparer.Ordinal);
    }
}
