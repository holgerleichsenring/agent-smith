using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-08-25-61f1: owns the decision row — what the agent chose, why, and where. Split
/// out of <see cref="RunEventApplier"/> so the table that holds an operator-facing record
/// also holds the rule that it is written once: a decision carries the trail position of
/// the event that logged it, and a replayed event adds nothing.
/// </summary>
public sealed class RunDecisionProjection(ProjectedEventRecords records)
{
    public async Task ApplyAsync(
        IUnitOfWork uow, DecisionLoggedEvent e, long? eventSeq, CancellationToken ct)
    {
        if (await records.HoldsAsync<RunDecision>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(new RunDecision
        {
            RunId = e.RunId, Name = e.Chose, Reason = e.Reason,
            StepIndex = e.OriginStepIndex, // p0388a
            Category = e.Category, // p0388c
            PhaseId = e.PhaseId, // p0466
            EventSeq = eventSeq,
        });
        await uow.SaveChangesAsync(ct);
    }
}
