using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// p0328: the applier's expectation projection, split out of
/// <see cref="RunEventApplier"/> like <see cref="RunCheckpointProjection"/>.
/// An ExpectationRatifiedEvent upserts the run's single expectation row
/// (unique RunId); a replayed event converges on the same row.
/// </summary>
internal static class RunExpectationProjection
{
    public static async Task UpsertAsync(
        IUnitOfWork uow, ExpectationRatifiedEvent e, CancellationToken ct)
    {
        var row = await uow.Set<RunExpectation>()
            .FirstOrDefaultAsync(x => x.RunId == e.RunId, ct);
        if (row is null)
        {
            row = new RunExpectation { RunId = e.RunId };
            uow.Add(row);
        }
        row.DraftJson = e.DraftJson;
        row.RatifiedJson = e.RatifiedJson;
        row.Outcome = e.Outcome;
        row.RatifiedBy = e.RatifiedBy;
        row.RatifiedAt = e.Timestamp;
        row.EditDistance = e.EditDistance;
        await uow.SaveChangesAsync(ct);
    }
}
