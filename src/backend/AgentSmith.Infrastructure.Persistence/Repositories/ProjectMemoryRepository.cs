using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-10-fcd7a: a project's memories over the relational store, served to singleton callers by opening a
/// scope per operation. An insert that loses to a concurrent writer of the same slug is retried once as an
/// update, so the newest write wins and one row stands.
/// </summary>
public sealed class ProjectMemoryRepository(IServiceScopeFactory scopeFactory, IUniqueViolationTranslator unique)
    : IProjectMemoryRepository
{
    public bool IsAvailable => true;

    public async Task<long?> ResolveProjectAsync(string project, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await Set<ConfigEntity>(scope).AsNoTracking()
            .Where(e => e.EntityType == UnmovedTicketRepository.ProjectDocType && e.EntityId == project)
            .Select(e => (long?)e.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectMemoryRecord>> ListAsync(long projectId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var rows = await Set<ProjectMemory>(scope).AsNoTracking()
            .Where(m => m.ProjectId == projectId).OrderByDescending(m => m.Id).ToListAsync(cancellationToken);
        return [.. rows.OrderByDescending(m => m.UpdatedAt.UtcDateTime).Select(RecordOf)];
    }

    public async Task UpsertAsync(long projectId, ProjectMemoryRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (ProjectMemoryLimits.Refusal(record) is { } refusal) throw new ArgumentException(refusal, nameof(record));
        try
        {
            await WriteAsync(projectId, record, cancellationToken);
        }
        catch (DbUpdateException ex) when (unique.IsUniqueViolation(ex))
        {
            await WriteAsync(projectId, record, cancellationToken);
        }
    }

    public async Task<bool> DeleteAsync(long projectId, string slug, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var row = await uow.Set<ProjectMemory>()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.Slug == slug, cancellationToken);
        if (row is null) return false;
        var key = ProjectMemorySource.LedgerKey(projectId);
        uow.Set<ProjectMemory>().Remove(row);
        uow.Set<MemoryEvidence>().RemoveRange(
            await uow.Set<MemoryEvidence>().Where(e => e.RepoKey == key && e.Slug == slug).ToListAsync(cancellationToken));
        await uow.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task WriteAsync(long projectId, ProjectMemoryRecord record, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var row = await uow.Set<ProjectMemory>()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.Slug == record.Slug, cancellationToken);
        if (row is null)
        {
            row = new ProjectMemory { ProjectId = projectId, Slug = record.Slug };
            uow.Set<ProjectMemory>().Add(row);
        }
        row.Type = record.Type;
        row.Description = record.Description;
        row.Body = record.Body;
        row.SourceKind = record.SourceKind;
        row.SourceId = record.SourceId;
        row.SourceRef = record.SourceRef;
        row.Author = record.Author;
        await uow.SaveChangesAsync(cancellationToken);
    }

    private static DbSet<T> Set<T>(IServiceScope scope) where T : class =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<T>();

    private static ProjectMemoryRecord RecordOf(ProjectMemory row) => new(
        row.Slug, row.Type, row.Description, row.Body, row.SourceKind, row.SourceId, row.SourceRef, row.Author,
        row.CreatedAt, row.UpdatedAt);
}
