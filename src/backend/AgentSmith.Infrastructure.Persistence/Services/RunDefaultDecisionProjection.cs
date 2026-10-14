using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-8762o: the default-decision family — one pending <see cref="RunDefaultDecision"/> per
/// <see cref="DefaultDecisionTakenEvent"/>, written once per trail position. A second memory proposal of one
/// file in a run is the same proposal (remember upserts by slug), so it is skipped while the first row is
/// pending. The projection only inserts: a replay never touches a verdict.
/// </summary>
public sealed class RunDefaultDecisionProjection(ProjectedEventRecords records) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(DefaultDecisionTakenEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        if (runEvent is not DefaultDecisionTakenEvent e) return;
        if (await records.HoldsAsync<RunDefaultDecision>(uow, e.RunId, eventSeq, ct)) return;
        if (e.Kind == DefaultDecisionKinds.MemoryProposal && await PendingPointerAsync(uow, e, ct)) return;
        uow.Add(RowOf(e, eventSeq));
        await uow.SaveChangesAsync(ct);
    }

    private static Task<bool> PendingPointerAsync(IUnitOfWork uow, DefaultDecisionTakenEvent e, CancellationToken ct) =>
        uow.Set<RunDefaultDecision>().AsNoTracking().AnyAsync(row =>
            row.RunId == e.RunId && row.Kind == DefaultDecisionKinds.MemoryProposal
            && row.PointerPath == e.PointerPath && row.Verdict == DefaultDecisionVerdicts.Pending, ct);

    internal static RunDefaultDecision RowOf(DefaultDecisionTakenEvent e, long? eventSeq) => new()
    {
        RunId = e.RunId, EventSeq = eventSeq, SpecId = e.SpecId, Question = e.Question, Chosen = e.Chosen,
        AlternativesJson = RunStoryJson.Serialize(e.Alternatives), Why = e.Why, HoldsSeries = e.HoldsSeries,
        Kind = e.Kind, PointerPath = e.PointerPath, TakenAt = e.Timestamp,
        Verdict = DefaultDecisionVerdicts.Pending, Conversation = e.Conversation,
    };
}
