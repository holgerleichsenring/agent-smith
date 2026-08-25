using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-08-25-e257: the operator's judgements of a run's criterion verdicts.
/// <para>
/// Recording the same criterion twice REPLACES: a judgement is a current opinion, not a
/// history nobody reads. Its row is separate from the acceptance snapshot on purpose — the
/// story applier assigns that payload wholesale on every publish, so a resume or a repair
/// pass would silently destroy anything stored inside it.
/// </para>
/// </summary>
public sealed class CriterionJudgementRepository(IUnitOfWork unitOfWork)
{
    /// <summary>
    /// The account and the judgements of it, together. Apart they are two fetches and two
    /// chances for a page to show a verdict whose correction has not arrived yet.
    /// </summary>
    public async Task<JudgedAcceptance> AcceptanceForRunAsync(string runId, CancellationToken ct)
    {
        var acceptanceJson = await unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.Id == runId).Select(r => r.AcceptanceJson).FirstOrDefaultAsync(ct);
        return new JudgedAcceptance(
            RunStoryJson.TryDeserialize<AcceptanceView>(acceptanceJson),
            await ForRunAsync(runId, ct));
    }

    public async Task<IReadOnlyList<CriterionJudgement>> ForRunAsync(
        string runId, CancellationToken ct) =>
        await unitOfWork.Set<RunCriterionJudgement>().AsNoTracking()
            .Where(j => j.RunId == runId)
            // By Id, not RecordedAt: SQLite cannot ORDER BY a DateTimeOffset, and the
            // identity is insertion order anyway.
            .OrderBy(j => j.Id)
            .Select(j => new CriterionJudgement(
                j.CriterionText, j.MachineStatus, j.HumanStatus, j.Reason, j.Author, j.RecordedAt))
            .ToListAsync(ct);

    /// <summary>Records one, replacing any earlier judgement of the same criterion.</summary>
    public async Task RecordAsync(
        string runId, CriterionJudgementRequest request, string author,
        DateTimeOffset recordedAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = CriterionKey.Of(request.Criterion);
        var existing = await unitOfWork.Set<RunCriterionJudgement>()
            .FirstOrDefaultAsync(j => j.RunId == runId && j.CriterionKey == key, ct);

        if (existing is null)
        {
            unitOfWork.Set<RunCriterionJudgement>().Add(new RunCriterionJudgement
            {
                RunId = runId,
                CriterionKey = key,
                CriterionText = request.Criterion,
                MachineStatus = request.MachineStatus,
                HumanStatus = request.HumanStatus,
                Reason = request.Reason,
                Author = author,
                RecordedAt = recordedAt,
            });
        }
        else
        {
            existing.CriterionText = request.Criterion;
            existing.MachineStatus = request.MachineStatus;
            existing.HumanStatus = request.HumanStatus;
            existing.Reason = request.Reason;
            existing.Author = author;
            existing.RecordedAt = recordedAt;
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Withdraws one. A judgement the operator no longer stands behind must be
    /// removable, or the corpus records what nobody believes.</summary>
    public async Task<bool> WithdrawAsync(string runId, string criterion, CancellationToken ct)
    {
        var key = CriterionKey.Of(criterion);
        var existing = await unitOfWork.Set<RunCriterionJudgement>()
            .FirstOrDefaultAsync(j => j.RunId == runId && j.CriterionKey == key, ct);
        if (existing is null) return false;
        unitOfWork.Remove(existing);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
