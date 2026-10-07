using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0393a: data access for the series pointer over a scoped unit of work. One row per
/// (Project, TicketKey) — a later revision of the same ticket upserts in place, because the
/// pointer answers "where is it and what did I last write", not "what happened".
/// </summary>
public sealed class TicketSeriesRepository(IUnitOfWork unitOfWork)
{
    public async Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken ct)
    {
        var row = await unitOfWork.Set<TicketSeries>().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Project == project && t.TicketKey == key, ct);
        return row is null ? null : ToPointer(row);
    }

    public async Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken ct)
    {
        var existing = await unitOfWork.Set<TicketSeries>()
            .FirstOrDefaultAsync(t => t.Project == project && t.TicketKey == pointer.Key, ct);
        if (existing is null)
        {
            existing = new TicketSeries { Project = project, TicketKey = pointer.Key };
            unitOfWork.Add(existing);
        }
        Apply(pointer, existing);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void Apply(SpecSetPointer pointer, TicketSeries row)
    {
        row.SeriesId = pointer.SeriesId ?? string.Empty;
        row.CarryingRepo = pointer.CarryingRepo;
        row.RevisionSha = pointer.RevisionSha;
        row.RevisionNumber = pointer.RevisionNumber;
        row.LastHandbackCase = (int)pointer.LastHandbackCase;
        row.RepeatedHandbackCount = pointer.RepeatedHandbackCount;
        row.ExecutedThrough = pointer.ExecutedThrough;
    }

    private static SpecSetPointer ToPointer(TicketSeries row) => new(
        row.TicketKey, row.CarryingRepo, row.RevisionSha, row.RevisionNumber,
        (SpecHandbackCase)row.LastHandbackCase, row.RepeatedHandbackCount,
        row.SeriesId.Length == 0 ? null : row.SeriesId, row.ExecutedThrough);
}
