using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-9a59e: what a published cut means for the store — one <see cref="RunSeriesCut"/> row per
/// publish event, and a not_started <see cref="RunSpec"/> row for every spec of the cut this run has
/// not executed, so a run's specs are readable before the first of them starts. A re-cut inside the
/// run removes the not_started rows it dropped; a row that already ran is never touched. A hand-back
/// cut carries no specs and leaves the rows as they are. Carried specs get no row: they are not this
/// run's work.
/// </summary>
public sealed class RunSeriesCutProjection(ProjectedEventRecords records)
{
    public const string NotStarted = "not_started";

    public async Task ApplyAsync(IUnitOfWork uow, SpecSetPublishedEvent e, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.SetKey is null || await records.HoldsAsync<RunSeriesCut>(uow, e.RunId, eventSeq, ct)) return;
        var specs = e.Specs ?? [];
        uow.Add(new RunSeriesCut
        {
            RunId = e.RunId, EventSeq = eventSeq, SetKey = e.SetKey, HandbackCase = e.HandbackCase,
            SpecsJson = RunStoryJson.Serialize(specs), At = e.Timestamp,
        });
        if (specs.Count > 0) await StandSpecsAsync(uow, e.RunId, Unexecuted(specs, e.CarriedSpecIds), ct);
        await uow.SaveChangesAsync(ct);
    }

    private static List<SpecCutEntry> Unexecuted(IReadOnlyList<SpecCutEntry> specs, IReadOnlyList<string>? carried) =>
        [.. specs.Where(s => carried is null || !carried.Contains(s.SpecId, StringComparer.Ordinal))];

    private static async Task StandSpecsAsync(
        IUnitOfWork uow, string runId, List<SpecCutEntry> unexecuted, CancellationToken ct)
    {
        var rows = await uow.Set<RunSpec>().Where(s => s.RunId == runId).ToListAsync(ct);
        foreach (var dropped in rows.Where(r => r.Status == NotStarted
                     && !unexecuted.Any(s => s.SpecId == r.SpecId)))
            uow.Remove(dropped);
        foreach (var spec in unexecuted)
        {
            var row = rows.FirstOrDefault(r => r.SpecId == spec.SpecId);
            if (row is null)
                uow.Add(new RunSpec
                {
                    RunId = runId, SpecId = spec.SpecId, Ordinal = spec.Ordinal, Title = spec.Goal, Status = NotStarted,
                });
            else if (row.Status == NotStarted)
                (row.Ordinal, row.Title) = (spec.Ordinal, spec.Goal);
        }
    }
}
