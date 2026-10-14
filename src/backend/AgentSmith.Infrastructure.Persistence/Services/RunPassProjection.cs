using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>2026-10-13-9a59f: one <see cref="RunPass"/> row per pass-end event, written once per trail position.</summary>
public sealed class RunPassProjection(ProjectedEventRecords records)
{
    public async Task ApplyAsync(IUnitOfWork uow, MasterPassEndedEvent e, long? eventSeq, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (await records.HoldsAsync<RunPass>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(new RunPass
        {
            RunId = e.RunId, EventSeq = eventSeq, SpecId = e.SpecId, Ordinal = e.Ordinal, PassInStep = e.PassInStep,
            OpenedBecause = e.OpenedBecause, EndedBecause = e.EndedBecause, ReengageReason = e.ReengageReason,
            VerdictEmitted = e.VerdictEmitted, CriteriaMet = e.CriteriaMet, CriteriaTotal = e.CriteriaTotal,
            CriteriaJson = e.Criteria is null ? null : RunStoryJson.Serialize(e.Criteria),
            GateLine = e.GateLine, ActionablePending = e.ActionablePending, UnbackedDone = e.UnbackedDone,
            EndedAt = e.Timestamp,
        });
        await uow.SaveChangesAsync(ct);
    }
}
