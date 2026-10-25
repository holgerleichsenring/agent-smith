using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-25-9324c: a person's judgement of a criterion as memory evidence. A not-met judgement contradicts
/// (-1, with the person's reason) every memory the criterion's spec relied on in that run; a judgement of any
/// other verdict, and a withdrawn one, retracts what that criterion contradicted. The criterion finds its spec
/// the way the ticket page joins them: the spec's acceptance carries the criterion's text, matched by
/// <see cref="CriterionKey.Of"/>. Only stages changes — the judgement's own save commits them with it.
/// </summary>
public sealed class JudgedMemoryEvidence
{
    public async Task RecordAsync(
        IUnitOfWork uow, string runId, string criterionKey, string humanStatus, string reason, DateTimeOffset at,
        CancellationToken ct)
    {
        await RetractAsync(uow, runId, criterionKey, ct);
        if (humanStatus != AcceptanceCriterionStatuses.Unmet) return;
        var specs = await SpecsJudgedAsync(uow, runId, criterionKey, ct);
        if (specs.Count == 0) return;
        var relied = await uow.Set<MemoryEvidence>().AsNoTracking()
            .Where(e => e.RunId == runId && e.Kind == MemoryEvidenceKinds.Relied && specs.Contains(e.SpecId))
            .ToListAsync(ct);
        foreach (var memory in relied)
            uow.Add(new MemoryEvidence
            {
                RepoKey = memory.RepoKey, Slug = memory.Slug, Kind = MemoryEvidenceKinds.Contradicted,
                Weight = MemoryStandingWeights.JudgedNotMet, Project = memory.Project, TicketId = memory.TicketId,
                RunId = runId, SpecId = memory.SpecId, Reason = reason, CriterionKey = criterionKey, At = at,
            });
    }

    public async Task RetractAsync(IUnitOfWork uow, string runId, string criterionKey, CancellationToken ct)
    {
        foreach (var row in await uow.Set<MemoryEvidence>()
                     .Where(e => e.RunId == runId && e.CriterionKey == criterionKey).ToListAsync(ct))
            uow.Remove(row);
    }

    private static async Task<List<string>> SpecsJudgedAsync(
        IUnitOfWork uow, string runId, string criterionKey, CancellationToken ct)
    {
        var specs = await uow.Set<RunSpec>().AsNoTracking().Where(s => s.RunId == runId && s.AcceptanceJson != null)
            .Select(s => new { s.SpecId, s.AcceptanceJson }).ToListAsync(ct);
        return [.. specs.Where(s => Names(RunStoryJson.TryDeserialize<AcceptanceView>(s.AcceptanceJson), criterionKey))
            .Select(s => s.SpecId)];
    }

    private static bool Names(AcceptanceView? acceptance, string criterionKey) =>
        acceptance is not null
        && (acceptance.Criteria.Any(c => CriterionKey.Of(c.Text) == criterionKey)
            || (acceptance.Declined ?? []).Any(d => CriterionKey.Of(d.Text) == criterionKey));
}
