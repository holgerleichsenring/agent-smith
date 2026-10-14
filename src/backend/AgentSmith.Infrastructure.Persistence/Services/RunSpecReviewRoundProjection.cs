using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-13-8762e: the review-round family — one <see cref="RunSpecReviewRound"/> per
/// <see cref="SpecReviewRoundEvent"/>, written once per trail position, so a replayed drain inserts nothing
/// twice. The one writer of the table.
/// </summary>
public sealed class RunSpecReviewRoundProjection(ProjectedEventRecords records) : IRunEventFamily
{
    public IReadOnlyCollection<Type> Events { get; } = [typeof(SpecReviewRoundEvent)];

    public async Task ApplyAsync(IUnitOfWork uow, RunEvent runEvent, long? eventSeq, CancellationToken ct)
    {
        if (runEvent is not SpecReviewRoundEvent e) return;
        if (await records.HoldsAsync<RunSpecReviewRound>(uow, e.RunId, eventSeq, ct)) return;
        uow.Add(RowOf(e, eventSeq));
        await uow.SaveChangesAsync(ct);
    }

    internal static RunSpecReviewRound RowOf(SpecReviewRoundEvent e, long? eventSeq) => new()
    {
        RunId = e.RunId, EventSeq = eventSeq, SpecId = e.SpecId, Scope = e.Scope, Round = e.Round,
        Reviewer = e.Reviewer, Verdict = e.Verdict, At = e.Timestamp, RevisionNumber = e.RevisionNumber,
        CutsJson = RunStoryJson.Serialize(e.Cuts),
        FindingsJson = RunStoryJson.Serialize(e.Findings),
        EvidenceProblemsJson = RunStoryJson.Serialize(e.EvidenceProblems),
        ResiduesJson = RunStoryJson.Serialize(e.Residues),
    };
}
