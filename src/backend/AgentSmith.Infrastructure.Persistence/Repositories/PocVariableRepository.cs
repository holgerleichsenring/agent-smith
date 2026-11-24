using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-933dg: the Poc row as the variable runs read it, and its plain variables written.</summary>
public sealed class PocVariableRepository(IUnitOfWork unitOfWork)
{
    public Task<PocVariableSubject?> ReadAsync(long pocId, CancellationToken ct) =>
        unitOfWork.Set<Poc>().AsNoTracking().Where(p => p.Id == pocId)
            .Select(p => new PocVariableSubject(p.Id, p.Name, p.Project, p.Target, p.SealFingerprint, p.PlainVariablesJson))
            .FirstOrDefaultAsync(ct);

    public Task WritePlainAsync(long pocId, string? json, CancellationToken ct) =>
        unitOfWork.Set<Poc>().Where(p => p.Id == pocId).ExecuteUpdateAsync(set => set.SetProperty(p => p.PlainVariablesJson, json), ct);
}
