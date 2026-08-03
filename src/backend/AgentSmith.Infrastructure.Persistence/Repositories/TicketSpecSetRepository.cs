using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0393a: data access for the work-spec pointer over a scoped unit of work. One
/// row per (Project, SpecKey) — a later revision of the same ticket upserts in
/// place, because the pointer answers "where is it and what did I last write",
/// not "what happened".
/// </summary>
public sealed class TicketSpecSetRepository(IUnitOfWork unitOfWork)
{
    public async Task<SpecSetPointer?> GetAsync(string project, string key, CancellationToken ct)
    {
        var row = await unitOfWork.Set<TicketSpecSet>().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Project == project && t.SpecKey == key, ct);
        return row is null ? null : ToPointer(row);
    }

    public async Task SaveAsync(string project, SpecSetPointer pointer, CancellationToken ct)
    {
        var existing = await unitOfWork.Set<TicketSpecSet>()
            .FirstOrDefaultAsync(t => t.Project == project && t.SpecKey == pointer.Key, ct);
        if (existing is null)
        {
            existing = new TicketSpecSet { Project = project, SpecKey = pointer.Key };
            unitOfWork.Add(existing);
        }
        Apply(pointer, existing);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void Apply(SpecSetPointer pointer, TicketSpecSet row)
    {
        row.CarryingRepo = pointer.CarryingRepo;
        row.RevisionSha = pointer.RevisionSha;
        row.RevisionNumber = pointer.RevisionNumber;
        row.LastHandbackCase = (int)pointer.LastHandbackCase;
        row.RepeatedHandbackCount = pointer.RepeatedHandbackCount;
        row.HandbackSourceSha = pointer.HandbackSourceSha;
    }

    private static SpecSetPointer ToPointer(TicketSpecSet row) => new(
        row.SpecKey, row.CarryingRepo, row.RevisionSha, row.RevisionNumber,
        (SpecHandbackCase)row.LastHandbackCase, row.RepeatedHandbackCount,
        row.HandbackSourceSha);
}
