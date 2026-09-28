using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads what the Criteria met figure is computed from: every finished run of the
/// <c>code</c> pipeline with its acceptance snapshot, and the operator's criterion
/// judgements of those runs. Aggregation happens in the server, not in SQL — the
/// snapshot is a JSON column and SQLite cannot bucket a DateTimeOffset.
/// </summary>
public sealed class CriteriaMetRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<JudgedCodeRun>> GetJudgedCodeRunsAsync(CancellationToken ct)
    {
        var runs = await FinishedCodeRuns()
            .Select(r => new { r.Id, r.Project, r.FinishedAt, r.AcceptanceJson })
            .ToListAsync(ct);
        var overrules = await OverrulesByRunAsync(ct);
        return runs
            .Select(r => new JudgedCodeRun(
                r.Project, r.FinishedAt!.Value,
                RunStoryJson.TryDeserialize<AcceptanceView>(r.AcceptanceJson),
                overrules[r.Id].ToList()))
            .ToList();
    }

    private IQueryable<Run> FinishedCodeRuns() =>
        unitOfWork.Set<Run>().AsNoTracking()
            .Where(r => r.Pipeline == PipelinePresets.CodeName
                        && r.FinishedAt != null && r.AcceptanceJson != null);

    // A join rather than a list of run ids: the id list of every coding run would outgrow
    // a provider's parameter limit long before the rows outgrow memory.
    private async Task<ILookup<string, CriterionOverrule>> OverrulesByRunAsync(CancellationToken ct)
    {
        var rows = await (from judgement in unitOfWork.Set<RunCriterionJudgement>().AsNoTracking()
                          join run in FinishedCodeRuns() on judgement.RunId equals run.Id
                          select new
                          {
                              judgement.RunId,
                              Overrule = new CriterionOverrule(
                                  judgement.CriterionKey, judgement.MachineStatus, judgement.HumanStatus),
                          })
            .ToListAsync(ct);
        return rows.ToLookup(r => r.RunId, r => r.Overrule);
    }
}
