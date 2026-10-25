using AgentSmith.Contracts.Memory;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Persistence.Extensions;

/// <summary>2026-10-25-9324a: the relational memory ledger replaces the DB-free null store.</summary>
public static class MemoryEvidenceExtensions
{
    public static IServiceCollection AddMemoryEvidence(this IServiceCollection services)
    {
        services.TryAddSingleton<MemoryEvidenceWriter>();
        services.RemoveAll<IMemoryEvidenceStore>();
        return services.AddSingleton<IMemoryEvidenceStore, MemoryEvidenceRepository>();
    }
}
