using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-10-25-9324a: the relational memory ledger, served to singleton callers by opening a scope per operation.
/// Read in insertion order: SQLite cannot ORDER BY a DateTimeOffset, and the identity is that order anyway.
/// </summary>
public sealed class MemoryEvidenceRepository(IServiceScopeFactory scopeFactory, MemoryEvidenceWriter writer)
    : IMemoryEvidenceStore
{
    public Task ReplaceAsync(
        string runId, string specId, string kind, IReadOnlyList<MemoryEvidenceFact> facts,
        CancellationToken cancellationToken) =>
        WithUnitOfWorkAsync(uow => writer.ReplaceAsync(uow, runId, specId, kind, facts, cancellationToken));

    public Task UpsertOutcomeAsync(MemoryEvidenceFact fact, CancellationToken cancellationToken) =>
        WithUnitOfWorkAsync(uow => writer.UpsertOutcomeAsync(uow, fact, cancellationToken));

    public Task AppendAsync(MemoryEvidenceFact fact, CancellationToken cancellationToken) =>
        WithUnitOfWorkAsync(uow => writer.AppendAsync(uow, fact, cancellationToken));

    public async Task<IReadOnlyList<MemoryEvidenceFact>> OfRepositoryAsync(
        string repoKey, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Set<MemoryEvidence>().AsNoTracking()
            .Where(e => e.RepoKey == repoKey).OrderBy(e => e.Id).ToListAsync(cancellationToken);
        return [.. rows.Select(MemoryEvidenceWriter.FactOf)];
    }

    private async Task WithUnitOfWorkAsync(Func<IUnitOfWork, Task> write)
    {
        using var scope = scopeFactory.CreateScope();
        await write(scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }
}
