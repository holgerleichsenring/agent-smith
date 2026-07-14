using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0329: reads production ratification outcomes for the expectation metric —
/// RunExpectations joined to Runs by RunId (children carry no FK, same as
/// RunRepository) so every outcome gains its project grouping key. One row
/// per run by construction (unique RunId upsert), so the whole set stays
/// small; aggregation happens in the server-side aggregator, not in SQL
/// (SQLite cannot translate DateTimeOffset arithmetic anyway).
/// </summary>
public sealed class ExpectationMetricsRepository(IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<ExpectationOutcomeRow>> GetOutcomeRowsAsync(
        CancellationToken ct) =>
        await (from expectation in unitOfWork.Set<RunExpectation>().AsNoTracking()
               join run in unitOfWork.Set<Run>().AsNoTracking()
                   on expectation.RunId equals run.Id
               select new ExpectationOutcomeRow(
                   run.Project, expectation.Outcome,
                   expectation.EditDistance, expectation.RatifiedAt))
            .ToListAsync(ct);
}
