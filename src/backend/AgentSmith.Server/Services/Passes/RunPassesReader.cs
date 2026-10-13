using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Passes;

/// <summary>
/// 2026-10-13-9a59f: a run's master passes per spec, from the pass-end rows, the ledger transitions and
/// the stamped model calls — four set queries, flat in the number of passes. A pass is known by its end
/// row or by calls stamped with its ordinal; one with calls and no end row died with its run.
/// </summary>
public sealed class RunPassesReader(IServiceScopeFactory scopes)
{
    /// <summary>Null when the run does not exist.</summary>
    public async Task<RunPassesView?> ReadAsync(string runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        if (!await uow.Set<Run>().AsNoTracking().AnyAsync(r => r.Id == runId, ct)) return null;
        var records = new RunPassRecords(
            await uow.Set<RunPass>().AsNoTracking().Where(p => p.RunId == runId).OrderBy(p => p.Id).ToListAsync(ct),
            await uow.Set<RunLedgerTransition>().AsNoTracking().Where(t => t.RunId == runId).OrderBy(t => t.Id).ToListAsync(ct),
            await uow.Set<RunLlmCall>().AsNoTracking().Where(c => c.RunId == runId)
                .Select(c => new RunPassCall(c.EventSeq, c.StepIndex, c.SpecId, c.MasterPass, c.CostUsd)).ToListAsync(ct),
            await uow.Set<RunStep>().AsNoTracking().Where(s => s.RunId == runId)
                .Select(s => new RunPassStep(s.EventSeq, s.StepIndex, s.CommandName)).ToListAsync(ct));
        return Compose(runId, records);
    }

    public static RunPassesView Compose(string runId, RunPassRecords records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var ended = records.Passes.GroupBy(p => p.Ordinal).ToDictionary(g => g.Key, g => g.Last());
        var stamped = records.Calls.Where(c => c.MasterPass is not null).ToLookup(c => c.MasterPass!.Value);
        var ordinals = ended.Keys.Union(stamped.Select(g => g.Key)).Order().ToList();
        var specOf = ordinals.ToDictionary(o => o, o => ended.GetValueOrDefault(o)?.SpecId
            ?? stamped[o].Select(c => c.SpecId).FirstOrDefault(s => s is not null) ?? string.Empty);
        var outside = records.Calls.Where(c => c.MasterPass is null && c.SpecId is not null).ToLookup(c => c.SpecId!);
        var specIds = ordinals.Select(o => specOf[o]).Concat(outside.Select(g => g.Key)).Distinct(StringComparer.Ordinal);

        var specs = specIds.Select(spec => new RunPassSpecView(
            spec,
            [.. ordinals.Where(o => specOf[o] == spec)
                .Select((o, i) => Pass(o, i + 1, ended.GetValueOrDefault(o), stamped[o], records.Transitions))],
            outside[spec].Aggregate(RunPassCostView.None, (sum, c) => sum.Add(c.CostUsd)))).ToList();
        var (derive, pr, other, none) = RunCallAttribution.Price(
            records.Calls.Where(c => c.MasterPass is null && c.SpecId is null), records.Steps);
        return new RunPassesView(runId, specs, derive, pr, other, none);
    }

    private static RunPassView Pass(
        int ordinal, int passInSpec, RunPass? row, IEnumerable<RunPassCall> calls, IReadOnlyList<RunLedgerTransition> transitions)
    {
        var cost = calls.Aggregate(RunPassCostView.None, (sum, c) => sum.Add(c.CostUsd));
        IReadOnlyList<LedgerTransitionView> moved = [.. transitions.Where(t => t.MasterPass == ordinal)
            .SelectMany(t => RunStoryJson.TryDeserialize<List<LedgerTransitionView>>(t.TransitionsJson) ?? [])];
        return new RunPassView(
            ordinal, passInSpec, row?.PassInStep, row?.OpenedBecause, row?.EndedBecause ?? MasterPassReasons.Unknown,
            row?.ReengageReason, row?.VerdictEmitted, row?.CriteriaMet, row?.CriteriaTotal,
            RunStoryJson.TryDeserialize<List<MasterPassCriterion>>(row?.CriteriaJson), row?.GateLine,
            row?.ActionablePending, row?.UnbackedDone, row?.EndedAt, moved, cost.CostUsd, cost.Calls);
    }
}
