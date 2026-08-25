using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services.Repair;

/// <summary>
/// 2026-08-25-61f1: names the runs an earlier replay recorded twice. Two trail rows at ONE
/// position is the objective marker — a run's trail position is minted once per event, so a
/// position held twice can only be a second projection of the same event.
/// <para>
/// It is also what bounds the repair. Rows that merely LOOK alike are collapsed only inside
/// a run this finder named; a run that was never replayed is never touched, however similar
/// two of its rows happen to be.
/// </para>
/// </summary>
public sealed class ReplayedRunFinder
{
    public async Task<IReadOnlyList<string>> FindAsync(IUnitOfWork uow, CancellationToken ct) =>
        await uow.Set<Entities.RunEvent>().AsNoTracking()
            .GroupBy(e => new { e.RunId, e.Seq })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.RunId)
            .Distinct()
            .ToListAsync(ct);
}
