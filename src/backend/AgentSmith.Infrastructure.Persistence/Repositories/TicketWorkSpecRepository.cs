using AgentSmith.Contracts.WorkSpecs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0390: data access for the work-spec pointer over a scoped unit of work. One
/// row per (Project, SpecKey) — a later revision of the same ticket upserts in
/// place, because the pointer answers "where is it and what did I last write",
/// not "what happened".
/// </summary>
public sealed class TicketWorkSpecRepository(IUnitOfWork unitOfWork)
{
    public async Task<WorkSpecPointer?> GetAsync(string project, string key, CancellationToken ct)
    {
        var row = await unitOfWork.Set<TicketWorkSpec>().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Project == project && t.SpecKey == key, ct);
        return row is null ? null : ToPointer(row);
    }

    public async Task SaveAsync(string project, WorkSpecPointer pointer, CancellationToken ct)
    {
        var existing = await unitOfWork.Set<TicketWorkSpec>()
            .FirstOrDefaultAsync(t => t.Project == project && t.SpecKey == pointer.Key, ct);
        if (existing is null)
        {
            existing = new TicketWorkSpec { Project = project, SpecKey = pointer.Key };
            unitOfWork.Add(existing);
        }
        Apply(pointer, existing);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void Apply(WorkSpecPointer pointer, TicketWorkSpec row)
    {
        row.CarryingRepo = pointer.CarryingRepo;
        row.RevisionSha = pointer.RevisionSha;
        row.RevisionNumber = pointer.RevisionNumber;
        row.LastHandbackCase = (int)pointer.LastHandbackCase;
        row.RepeatedHandbackCount = pointer.RepeatedHandbackCount;
        row.HandbackSourceSha = pointer.HandbackSourceSha;
    }

    private static WorkSpecPointer ToPointer(TicketWorkSpec row) => new(
        row.SpecKey, row.CarryingRepo, row.RevisionSha, row.RevisionNumber,
        (WorkSpecHandbackCase)row.LastHandbackCase, row.RepeatedHandbackCount,
        row.HandbackSourceSha);
}
